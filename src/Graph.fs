[<RequireQualifiedAccess>]
module Worker.Graph

open System
open Infrastructure
open Worker.Domain

let private defaultWorkdays =
    set
        [ DayOfWeek.Monday
          DayOfWeek.Tuesday
          DayOfWeek.Wednesday
          DayOfWeek.Thursday
          DayOfWeek.Friday
          DayOfWeek.Saturday
          DayOfWeek.Sunday ]

let private parseWorkdays (workdays: string) =
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
    | _ -> Ok defaultWorkdays

let private parseTimeSpan (value: string) =
    match value with
    | AP.IsString str ->
        match str with
        | AP.IsTimeSpan value -> Ok <| Some value
        | _ -> Error <| NotSupported "TimeSpan. Expected format: 'dd.hh:mm:ss'."
    | _ -> Ok None

let private parseLimit (limit: int) =
    if limit <= 0 then None else Some <| uint limit

let private mapSchedule (schedule: External.Schedule) =
    match schedule.IsEnabled with
    | false -> Ok None
    | true ->
        schedule.Workdays
        |> parseWorkdays
        |> Result.bind (fun workdays ->
            schedule.Delay
            |> parseTimeSpan
            |> Result.map (fun delay ->
                Some
                    { StartWork = Option.ofNullable schedule.StartWork |> Option.defaultValue DateTime.UtcNow
                      StopWork = Option.ofNullable schedule.StopWork
                      Workdays = workdays
                      Delay = delay
                      Limit = schedule.Limit |> parseLimit
                      TimeShift = schedule.TimeShift }))

let private mapTask (task: External.TaskGraph) handler =
    task.Schedule
    |> mapSchedule
    |> Result.bind (fun schedule ->
        task.Duration
        |> parseTimeSpan
        |> Result.map (fun duration ->
            { Name = task.Name
              Parallel = task.Parallel
              Recursively = task.Recursively
              Duration = duration
              Schedule = schedule
              Handler = handler }))

let create rootNode graph =
    let getTaskHandler nodeName node =
        node |> Graph.findNode nodeName |> Option.bind (_.Value.Task)

    let createNode nodeName innerLoop (taskGraph: External.TaskGraph) =
        let taskName = nodeName |> Graph.buildNodeName <| taskGraph.Name

        innerLoop (Some taskName) taskGraph.Steps
        |> Result.bind (fun steps ->
            let handler = rootNode |> getTaskHandler taskName

            mapTask taskGraph handler |> Result.map (fun task -> Graph.Node(task, steps)))

    let rec innerLoop name tasks =
        match tasks with
        | [||] -> Ok []
        | null -> Ok []
        | _ -> tasks |> Array.map (createNode name innerLoop) |> Seq.roe

    graph |> createNode None innerLoop
