module Worker.Mapper

open System
open Infrastructure
open Infrastructure.Domain.Errors
open Infrastructure.DSL
open Infrastructure.DSL.AP
open Infrastructure.Domain.Graph
open Domain.Internal

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
    | IsString str ->
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
                    <| Parsing "Workday is not valid. Expected values: 'mon', 'tue', 'wed', 'thu', 'fri', 'sat', 'sun'.")
            |> DSL.Seq.roe
            |> Result.map Set.ofList
    | _ -> Ok defaultWorkdays

let private parseTimeSpan (value: string) =
    match value with
    | IsString str ->
        match str with
        | IsTimeSpan value -> Ok <| Some value
        | _ -> Error <| Parsing "Time value is not valid. Expected format: 'dd.hh:mm:ss'."
    | _ -> Ok None

let private parseLimit (limit: int) =
    if limit <= 0 then None else Some <| uint limit

let private mapSchedule (schedule: Domain.External.Schedule) =
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

let private mapTask (task: Domain.External.Task) (handle: HandleTask) =
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
              Handle = handle }))

let buildCoreGraph (task: Domain.External.Task) handlersGraph =
    let getHandleFun nodeName graph =
        graph |> Graph.findNode nodeName |> Option.bind (_.Value.Handle)

    let createNode innerLoop nodeName (task: Domain.External.Task) =
        let taskName = nodeName |> Graph.buildNodeName <| task.Name

        innerLoop (Some taskName) task.Steps
        |> Result.bind (fun steps ->
            let handle = handlersGraph |> getHandleFun taskName

            mapTask task handle |> Result.map (fun task -> Node(task, steps)))

    let rec innerLoop nodeName (tasks: Domain.External.Task array) =
        match tasks with
        | [||] -> Ok []
        | null -> Ok []
        | _ -> tasks |> Array.map (createNode innerLoop nodeName) |> DSL.Seq.roe

    task |> createNode innerLoop None
