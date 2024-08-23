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
        | AP.IsTimeSpan value -> Ok value
        | _ -> Error <| NotSupported "TimeSpan. Expected format: 'dd.hh:mm:ss'."
    | _ -> Error <| NotSupported "TimeSpan. Expected format: 'dd.hh:mm:ss'."

let private parseRecursively (recursively: External.TaskRecursion) =
    recursively.Delay
    |> parseTimeSpan
    |> Result.map(fun delay ->
        { Delay = delay
          Await = recursively.Await })

let private setLimit (limit: int) =
    if limit <= 0 then None else Some <| uint limit

let private mapSchedule (schedule: External.Schedule) =
    schedule.Workdays
    |> parseWorkdays
    |> Result.map (fun workdays ->
        Some <| 
            { StartWork = schedule.StartWork |> Option.defaultValue DateTime.UtcNow
              StopWork = schedule.StopWork
              Workdays = workdays
              TimeShift = schedule.TimeShift })

let private parseHandler taskEnabled (handler: TaskHandler option) taskName =
    match taskEnabled, handler with
    | true, None -> Error <| NotFound $"Required handler of the task '%s{taskName}'."
    | true, Some handler -> Ok <| Some handler
    | false, _ -> Ok None


let private mapTask (task: External.TaskGraph) handler =
    let schedule = task.Schedule |> Option.map mapSchedule
    let duration = task.Duration |> Option.map parseTimeSpan
    let recursively = task.Recursively |> Option.map parseRecursively
    let handler = task.Name |> parseHandler task.Enabled handler



   
    

let create rootNode graph =
    let getTaskHandler nodeName node =
        node |> Graph.findNode nodeName |> Option.bind (_.Value.Task)

    let createNode nodeName innerLoop (taskGraph: External.TaskGraph) =
        let taskName = nodeName |> Graph.buildNodeName <| taskGraph.Name

        innerLoop (Some taskName) taskGraph.Tasks
        |> Result.bind (fun tasks ->
            let handler = rootNode |> getTaskHandler taskName

            mapTask taskGraph handler |> Result.map (fun task -> Graph.Node(task, tasks)))

    let rec innerLoop name tasks =
        match tasks with
        | [||] -> Ok []
        | null -> Ok []
        | _ -> tasks |> Array.map (createNode name innerLoop) |> Seq.roe

    graph |> createNode None innerLoop
