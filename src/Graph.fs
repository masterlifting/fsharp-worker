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

let rec private createNode' (taskGraph: External.TaskGraph) taskHandlerRes (nodes: Graph.Node<Task> list)=
    match taskHandlerRes with
    | Ok None -> 
    | Ok (Some handler) -> mapTask taskGraph handler |> Result.map (fun task -> Graph.Node(task, nodes))
    | Error error -> Error error

let create rootNode graph =
    let createNode nodeName innerLoop (taskGraph: External.TaskGraph) =
        let taskName = nodeName |> Graph.buildNodeName <| taskGraph.Name
        let taskHandler = rootNode |> Graph.findNode taskName |> Option.bind (_.Value.Task) |> validateHandler taskName taskGraph.Enabled

        innerLoop (Some taskName) taskGraph.Tasks
        |> Result.bind (createNode' taskGraph taskHandler)

    let rec innerLoop name graphTasks =
        match graphTasks with
        | [||] -> Ok []
        | null -> Ok []
        | _ -> graphTasks |> Array.map (createNode name innerLoop) |> Seq.roe

    graph |> createNode None innerLoop
