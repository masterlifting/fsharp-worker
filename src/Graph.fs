[<RequireQualifiedAccess>]
module Worker.Graph

open System
open Infrastructure
open Worker.Domain

let private DefaultWorkdays =
    set
        [ DayOfWeek.Monday
          DayOfWeek.Tuesday
          DayOfWeek.Wednesday
          DayOfWeek.Thursday
          DayOfWeek.Friday
          DayOfWeek.Saturday
          DayOfWeek.Sunday ]

let private parseWorkdays workdays =
    match workdays with
    | AP.IsString str ->
        match str.Split(",") with
        | data ->
            data
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
            |> Seq.roe
            |> Result.map Set.ofList
    | _ -> Ok DefaultWorkdays

let private parseDateOnly day =
    if String.IsNullOrEmpty day then
        Ok <| DateOnly.FromDateTime(DateTime.UtcNow)
    else
        match day with
        | AP.IsDateOnly value -> Ok value
        | _ -> Error <| NotSupported "DateOnly. Expected format: 'yyyy-MM-dd'."

let private parseTimeOnly time =
    if String.IsNullOrEmpty time then
        Ok <| TimeOnly.FromDateTime(DateTime.UtcNow)
    else
        match time with
        | AP.IsTimeOnly value -> Ok value
        | _ -> Error <| NotSupported "TimeOnly. Expected format: 'hh:mm:ss'."

let private validatedModel = ModelBuilder()

let private mapSchedule (schedule: External.Schedule) =
    validatedModel {
        let! workdays = schedule.Workdays |> parseWorkdays
        let! startDate = schedule.StartDate |> parseDateOnly
        let! stopDate = schedule.StopDate |> Option.toResult parseDateOnly
        let! startTime = schedule.StartTime |> parseTimeOnly
        let! stopTime = schedule.StopTime |> Option.toResult parseTimeOnly

        return
            { StartDate = startDate
              StopDate = stopDate
              StartTime = startTime
              StopTime = stopTime
              Workdays = workdays
              TimeShift = schedule.TimeShift }
    }

let private parseTimeSpan value =
    match value with
    | AP.IsString str ->
        match str with
        | AP.IsTimeSpan value -> Ok value
        | _ -> Error <| NotSupported "TimeSpan. Expected format: 'dd.hh:mm:ss'."
    | _ -> Error <| NotSupported "TimeSpan. Expected format: 'dd.hh:mm:ss'."

let private validateHandler taskName taskEnabled (handler: TaskHandler option) =
    match taskEnabled, handler with
    | true, None -> Error <| NotFound $"Handler for task '{taskName}'."
    | true, Some handler -> Ok <| Some handler
    | false, _ -> Ok None

let private mapTask (task: External.TaskGraph) handler =
    validatedModel {
        let! recursively = task.Recursively |> Option.toResult parseTimeSpan
        let! duration = task.Duration |> Option.toResult parseTimeSpan
        let! schedule = task.Schedule |> Option.toResult mapSchedule

        return
            { Name = task.Name
              Parallel = task.Parallel
              Recursively = recursively
              Duration = duration
              Wait = task.Wait
              Schedule = schedule
              Handler = handler }
    }

let create rootNode graph =
    
    let toTaskNode (task: External.TaskGraph) handler=
        Result.bind(fun nodes ->
            handler
            |> Result.bind (mapTask task >> Result.map (fun task -> Graph.Node(task, nodes))))

    let createResult nodeName toTaskNodes (graph: External.TaskGraph) =
        let taskName = nodeName |> Graph.buildNodeName <| graph.Name
        let taskHandler = rootNode |> Graph.findNode taskName |> Option.bind (_.Value.Task) |> validateHandler taskName graph.Enabled

        graph.Tasks 
        |> toTaskNodes (Some taskName)
        |> toTaskNode graph taskHandler

    let rec toTaskNodes name tasks =
        match tasks with
        | [||] -> Ok []
        | null -> Ok []
        | _ -> tasks |> Array.map (createResult name toTaskNodes) |> Seq.roe

    graph |> createResult None toTaskNodes
