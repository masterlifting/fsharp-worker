[<RequireQualifiedAccess>]
module Worker.Graph

open System
open Infrastructure
open Worker.Domain

let private parseWorkdays workdays =
    match workdays with
    | AP.IsString str ->
        str.Split ','
        |> Array.map (function
            | "mon" -> Ok DayOfWeek.Monday
            | "tue" -> Ok DayOfWeek.Tuesday
            | "wed" -> Ok DayOfWeek.Wednesday
            | "thu" -> Ok DayOfWeek.Thursday
            | "fri" -> Ok DayOfWeek.Friday
            | "sat" -> Ok DayOfWeek.Saturday
            | "sun" -> Ok DayOfWeek.Sunday
            | _ ->
                Error
                <| NotSupported "Workday. Expected values: 'mon', 'tue', 'wed', 'thu', 'fri', 'sat', 'sun'.")
        |> Result.choose
        |> Result.map Set.ofList
    | _ ->
        Ok
        <| set
            [ DayOfWeek.Monday
              DayOfWeek.Tuesday
              DayOfWeek.Wednesday
              DayOfWeek.Thursday
              DayOfWeek.Friday
              DayOfWeek.Saturday
              DayOfWeek.Sunday ]

let private parseDateOnly day =
    match day with
    | AP.IsDateOnly value -> Ok value
    | _ -> Error <| NotSupported "DateOnly. Expected format: 'yyyy-MM-dd'."

let private parseTimeOnly time =
    match time with
    | AP.IsTimeOnly value -> Ok value
    | _ -> Error <| NotSupported "TimeOnly. Expected format: 'hh:mm:ss'."

let private scheduleResult = ResultBuilder()

let private mapSchedule (schedule: External.Schedule) =
    scheduleResult {
        let! workdays = schedule.Workdays |> parseWorkdays
        let! startDate = schedule.StartDate |> Option.toResult parseDateOnly
        let! stopDate = schedule.StopDate |> Option.toResult parseDateOnly
        let! startTime = schedule.StartTime |> Option.toResult parseTimeOnly
        let! stopTime = schedule.StopTime |> Option.toResult parseTimeOnly

        return
            { StartDate = startDate
              StopDate = stopDate
              StartTime = startTime
              StopTime = stopTime
              Workdays = workdays
              TimeShift = schedule.TimeShift }
    }

let private parseTimeSpan timeSpan =
    match timeSpan with
    | AP.IsTimeSpan value -> Ok value
    | _ -> Error <| NotSupported "TimeSpan. Expected format: 'dd.hh:mm:ss'."

let private validateHandler taskName taskEnabled (handler: TaskHandler option) =
    match taskEnabled, handler with
    | true, None -> Error <| NotFound $"Handler for task '%s{taskName}'."
    | true, Some handler -> Ok <| Some handler
    | false, _ -> Ok None

let private mapTask (task: External.TaskGraph) handler =
    scheduleResult {
        let! recursively = task.Recursively |> Option.toResult parseTimeSpan
        let! duration = task.Duration |> Option.toResult parseTimeSpan
        let! schedule = task.Schedule |> Option.toResult mapSchedule

        return
            { Name = task.Name
              Parallel = task.Parallel
              Recursively = recursively
              Duration = duration |> Option.defaultValue (TimeSpan.FromMinutes 5.)
              Wait = task.Wait
              Schedule = schedule
              Handler = handler }
    }

let create rootNode graph =

    let toNode task handler =
        Result.bind (fun nodes -> mapTask task handler |> Result.map (fun task -> Graph.Node(task, nodes)))

    let createResult nodeName toListNodes (graph: External.TaskGraph) =
        let taskName = nodeName |> Graph.buildNodeName <| graph.Name

        rootNode
        |> Graph.findNode taskName
        |> Option.bind (_.Value.Task)
        |> validateHandler taskName graph.Enabled
        |> Result.bind (fun handler -> graph.Tasks |> toListNodes (Some taskName) |> toNode graph handler)

    let rec toListNodes name tasks =
        match tasks with
        | [||]
        | null -> Ok []
        | _ -> tasks |> Array.map (createResult name toListNodes) |> Result.choose

    graph |> createResult None toListNodes

let map graph =

    let toNode task =
        Result.bind (fun nodes -> mapTask task None |> Result.map (fun task -> Graph.Node(task, nodes)))

    let createResult nodeName toListNodes (graph: External.TaskGraph) =

        let taskName = nodeName |> Graph.buildNodeName <| graph.Name
        graph.Tasks |> toListNodes (Some taskName) |> toNode graph

    let rec toListNodes name tasks =
        match tasks with
        | [||]
        | null -> Ok []
        | _ -> tasks |> Array.map (createResult name toListNodes) |> Result.choose

    graph |> createResult None toListNodes
