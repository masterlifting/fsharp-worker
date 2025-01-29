module internal Worker.Scheduler

open System
open Infrastructure.Prelude
open Worker.Domain

let rec private findNearestWorkday (date: DateOnly) workdays =
    match workdays = Set.empty with
    | true -> None
    | false ->
        match workdays.Contains(date.DayOfWeek) with
        | true -> Some date
        | false -> workdays |> findNearestWorkday (date.AddDays 1)

let private compute (now: DateTime) schedule =
    let startDateTime =
        match schedule.StartDate, schedule.StartTime with
        | Some startDate, Some startTime -> startDate.ToDateTime startTime
        | Some startDate, None -> startDate.ToDateTime TimeOnly.MinValue
        | None, Some startTime -> now.Date.Add(startTime.ToTimeSpan())
        | None, None -> now

    let stopDateTime =
        match schedule.StopDate, schedule.StopTime with
        | Some stopDate, Some stopTime -> stopDate.ToDateTime stopTime |> Some
        | Some stopDate, None -> stopDate.ToDateTime TimeOnly.MinValue |> Some
        | None, Some stopTime -> now.Date.Add(stopTime.ToTimeSpan()) |> Some
        | None, None -> None

    let today = DateOnly.FromDateTime now

    let toDelay dateTime = dateTime - now

    let toNearestWorkday date =
        schedule.Workdays |> findNearestWorkday date

    let handleStart () =
        match startDateTime > now with
        | true -> StartIn(startDateTime |> toDelay, schedule)
        | false -> Started schedule

    match today |> toNearestWorkday with
    | None ->
        match stopDateTime with
        | None -> handleStart ()
        | Some stopDateTime ->
            match stopDateTime > now with
            | true -> StopIn(stopDateTime |> toDelay, schedule)
            | false ->
                match schedule.Recursively with
                | false -> Stopped(StopTimeReached stopDateTime)
                | true ->
                    let tomorrow = today.AddDays 1
                    let nextDate = tomorrow |> toNearestWorkday |> Option.defaultValue tomorrow

                    let nextStartDateTime =
                        nextDate.ToDateTime(schedule.StartTime |> Option.defaultValue TimeOnly.MinValue)

                    StartIn(nextStartDateTime |> toDelay, schedule)
    | Some nearestWorkday ->
        match nearestWorkday.DayOfWeek = today.DayOfWeek with
        | true -> handleStart ()
        | false ->
            match schedule.Recursively with
            | false -> Stopped(NotWorkday today.DayOfWeek)
            | true ->
                let nextStartDateTime =
                    nearestWorkday.ToDateTime(schedule.StartTime |> Option.defaultValue TimeOnly.MinValue)

                StartIn(toDelay nextStartDateTime, schedule)

let private merge parent current =
    fun withContinue ->
        match parent, current with
        | None, None -> None
        | Some parent, None -> Some parent
        | None, Some current -> Some current
        | Some parent, Some current ->
            { parent with
                Workdays = parent.Workdays |> Set.intersect current.Workdays
                StartDate = Option.max parent.StartDate current.StartDate
                StopDate = Option.min parent.StopDate current.StopDate
                StartTime = Option.max parent.StartTime current.StartTime
                StopTime = Option.min parent.StopTime current.StopTime
                Recursively = parent.Recursively || current.Recursively
                TimeZone = current.TimeZone }
            |> Some
        |> Option.map (fun schedule ->
            { schedule with
                Recursively = withContinue || schedule.Recursively })

let set parentSchedule currentSchedule withContinue =
    let mergeSchedules = parentSchedule |> merge <| currentSchedule

    match mergeSchedules withContinue with
    | None -> NotScheduled
    | Some schedule ->
        let now = DateTime.UtcNow.AddHours(schedule.TimeZone |> float)
        schedule |> compute now
