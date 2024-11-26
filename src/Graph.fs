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
                "Workday. Expected values: 'mon,tue,wed,thu,fri,sat,sun'."
                |> NotSupported
                |> Error)
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
    | _ -> "DateOnly. Expected format: 'yyyy-MM-dd'." |> NotSupported |> Error

let private parseTimeOnly time =
    match time with
    | AP.IsTimeOnly value -> Ok value
    | _ -> "TimeOnly. Expected format: 'hh:mm:ss'." |> NotSupported |> Error

let private result = ResultBuilder()

let private mapSchedule (schedule: External.Schedule) =
    result {
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
              TimeZone = schedule.TimeZone }
    }

let private parseTimeSpan timeSpan =
    match timeSpan with
    | AP.IsTimeSpan value -> Ok value
    | _ -> "TimeSpan. Expected format: 'dd.hh:mm:ss'." |> NotSupported |> Error

let private toWorkerTask handler enabled (task: External.TaskGraph) =
    result {
        let! recursively = task.Recursively |> Option.toResult parseTimeSpan
        let! duration = task.Duration |> Option.toResult parseTimeSpan
        let! schedule = task.Schedule |> Option.toResult mapSchedule

        return
            { Id = handler.Id
              Name = task.Name
              Parallel = task.Parallel
              Recursively = recursively
              Duration = duration |> Option.defaultValue (TimeSpan.FromMinutes 5.)
              Wait = task.Wait
              Schedule = schedule
              Handler =
                match enabled with
                | true -> handler.Task
                | false -> None }
    }

let merge (handlers: Graph.Node<WorkerHandler>) taskGraph =

    let rec mergeLoop taskName (taskGraph: External.TaskGraph) =
        let fullTaskName = taskGraph.Name |> Graph.buildNodeName taskName

        match handlers |> Graph.BFS.tryFindByName fullTaskName with
        | None -> $"%s{fullTaskName} handler" |> NotFound |> Error
        | Some handler ->
            taskGraph
            |> toWorkerTask handler.Value taskGraph.Enabled
            |> Result.bind (fun workerTask ->
                match taskGraph.Tasks with
                | null -> Graph.Node(workerTask, []) |> Ok
                | tasks ->
                    tasks
                    |> Array.map (mergeLoop (Some fullTaskName))
                    |> Result.choose
                    |> Result.map (fun children -> Graph.Node(workerTask, children)))

    taskGraph |> mergeLoop None
