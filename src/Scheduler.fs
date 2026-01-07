module internal Worker.Scheduler

open System
open Infrastructure.Prelude
open Worker.Domain
open Infrastructure.Logging

[<Literal>]
let ModuleName = "[Worker.Scheduler]"

let private compute (now: DateTime) (schedule: Schedule) =

    Log.dbg $"{ModuleName} Computing schedule at {now} with schedule {schedule}"

    let today = DateOnly.FromDateTime now

    let toDelay dateTime = dateTime - now

    let rec toNearestWorkday (date: DateOnly) =
        match schedule.Workdays.Contains date.DayOfWeek with
        | true -> date
        | false -> date.AddDays 1 |> toNearestWorkday

    let startDate = schedule.StartDate |> Option.defaultValue today |> toNearestWorkday
    let startTime = schedule.StartTime |> Option.defaultValue TimeOnly.MinValue
    let startDateTime = startDate.ToDateTime startTime

    let stopDateTime =
        match schedule.StopDate, schedule.StopTime with
        | Some stopDate, Some stopTime -> stopDate.ToDateTime stopTime |> Some
        | Some stopDate, None -> stopDate.ToDateTime TimeOnly.MinValue |> Some
        | None, Some stopTime -> today.ToDateTime stopTime |> Some
        | None, None -> None

    let toNextStopDateTime (stopDateTime: DateTime) =
        let stopDate = DateOnly.FromDateTime stopDateTime
        let stopTime = TimeOnly.FromDateTime stopDateTime
        let nextStopDate = stopDate.AddDays 1 |> toNearestWorkday
        nextStopDate.ToDateTime stopTime

    let handleStopDate stopDateTime =
        match startDateTime, stopDateTime, now with
        | _ when startDateTime > now && startDateTime > stopDateTime ->
            let nextStopDateTime = stopDateTime |> toNextStopDateTime

            match startDateTime > nextStopDateTime with
            | true -> Stopped(StartTimeCannotBeReached startDateTime)
            | false -> StartIn(startDateTime |> toDelay, schedule)
        | _ when startDateTime > now ->
            let nextStartDate = startDate |> toNearestWorkday
            let nextStartDateTime = nextStartDate.ToDateTime startTime

            StartIn(nextStartDateTime |> toDelay, schedule)
        | _ when startDateTime > stopDateTime -> Stopped(StartTimeCannotBeReached startDateTime)
        | _ when stopDateTime <= now -> Stopped(StopTimeReached stopDateTime)
        | _ -> StopIn(stopDateTime |> toDelay, schedule)

    let rec handleStopDateRecursively startDateTime stopDateTime =
        match startDateTime, stopDateTime, now with
        | _ when startDateTime < stopDateTime && startDateTime <= now && stopDateTime > now -> Started schedule
        | _ when startDateTime > now -> StartIn(startDateTime |> toDelay, schedule)
        | _ ->
            let nextStartDateTime =
                match startDateTime > stopDateTime with
                | true -> startDateTime
                | false ->
                    let nextStartDate = startDate.AddDays 1 |> toNearestWorkday
                    nextStartDate.ToDateTime startTime

            let nextStopDateTime = stopDateTime |> toNextStopDateTime
            handleStopDateRecursively nextStartDateTime nextStopDateTime

    match schedule.Workdays.IsEmpty with
    | true -> Stopped EmptyWorkdays
    | false ->
        match stopDateTime with
        | None ->
            match startDateTime > now with
            | true -> StartIn(startDateTime |> toDelay, schedule)
            | false -> Started schedule
        | Some stopDateTime ->
            match schedule.StopDate.IsSome with
            | true -> stopDateTime |> handleStopDate
            | false ->
                match schedule.Recursively.IsSome with
                | false -> stopDateTime |> handleStopDate
                | true -> handleStopDateRecursively startDateTime stopDateTime

let private merge parent current =
    match parent, current with
    | None, None -> None
    | Some parent, None -> Some parent
    | None, Some current -> Some current
    | Some parent, Some current ->
        {
            Name = current.Name
            Workdays = parent.Workdays |> Set.intersect current.Workdays
            StartDate = Option.max parent.StartDate current.StartDate
            StopDate = Option.min parent.StopDate current.StopDate
            StartTime = Option.max parent.StartTime current.StartTime
            StopTime = Option.min parent.StopTime current.StopTime
            TimeZone = current.TimeZone
            Recursively =
                match parent.Recursively.IsSome with
                | true -> parent.Recursively
                | false -> current.Recursively
        }
        |> Some

let internal set' parentSchedule currentSchedule (now: DateTime) =
    match parentSchedule |> merge <| currentSchedule with
    | None -> NotScheduled
    | Some schedule -> schedule |> compute (now.AddHours(float schedule.TimeZone))

let set parentSchedule currentSchedule =
    set' parentSchedule currentSchedule DateTime.UtcNow
