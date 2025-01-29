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
    let today = DateOnly.FromDateTime now
    let tomorrow = today.AddDays 1
    let startDate = schedule.StartDate |> Option.defaultValue today

    let startDateTime =
        match schedule.StartTime with
        | Some startTime -> startDate.ToDateTime startTime
        | None -> startDate.ToDateTime TimeOnly.MinValue

    let stopDateTime =
        match schedule.StopDate, schedule.StopTime with
        | Some stopDate, Some stopTime -> stopDate.ToDateTime stopTime |> Some
        | Some stopDate, None -> stopDate.ToDateTime TimeOnly.MinValue |> Some
        | None, Some stopTime -> today.ToDateTime stopTime |> Some
        | None, None -> None

    let toDelay dateTime = dateTime - now

    let toNearestWorkday date =
        schedule.Workdays |> findNearestWorkday date

    let toStartDateTime (date: DateOnly) =
        date.ToDateTime(schedule.StartTime |> Option.defaultValue TimeOnly.MinValue)

    let toStopDateTime (date: DateOnly) =
        date.ToDateTime(schedule.StopTime |> Option.defaultValue TimeOnly.MinValue)

    let nextDate =
        match startDate > tomorrow with
        | true -> startDate |> toNearestWorkday |> Option.defaultValue startDate
        | false -> tomorrow |> toNearestWorkday |> Option.defaultValue tomorrow

    let handleStart () =
        match startDateTime > now with
        | true -> StartIn(startDateTime |> toDelay, schedule)
        | false -> Started schedule

    let handleStop stopDateTime =
        match stopDateTime > now with
        | true ->
            match startDateTime < stopDateTime with
            | true ->
                match startDateTime > now with
                | true -> StartIn(startDateTime |> toDelay, schedule)
                | false -> StopIn(stopDateTime |> toDelay, schedule)
            | false ->
                match schedule.StopDate.IsSome with
                | true -> Stopped(StartTimeCannotBeReached startDateTime)
                | false ->
                    let nextStartDateTime = nextDate |> toStartDateTime
                    let nextStopDateTime = nextDate |> toStopDateTime

                    match nextStartDateTime > nextStopDateTime with
                    | true -> Stopped(StartTimeCannotBeReached nextStartDateTime)
                    | false -> StartIn(nextStartDateTime |> toDelay, schedule)
        | false ->
            match schedule.StopDate.IsSome with
            | true -> Stopped(StopTimeReached stopDateTime)
            | false ->
                match schedule.Recursively with
                | false -> Stopped(StopTimeReached stopDateTime)
                | true ->
                    match startDateTime > stopDateTime with
                    | true ->
                        match schedule.StopDate.IsSome with
                        | true -> Stopped(StartTimeCannotBeReached startDateTime)
                        | false ->
                            let nextStartDateTime = nextDate |> toStartDateTime
                            let nextStopDateTime = nextDate |> toStopDateTime

                            match nextStartDateTime > nextStopDateTime with
                            | true -> Stopped(StartTimeCannotBeReached nextStartDateTime)
                            | false -> StartIn(nextStartDateTime |> toDelay, schedule)
                    | false -> StartIn(nextDate |> toStartDateTime |> toDelay, schedule)

    match today |> toNearestWorkday with
    | None ->
        match stopDateTime with
        | None -> handleStart ()
        | Some stopDateTime -> stopDateTime |> handleStop
    | Some nearestWorkday ->
        match nearestWorkday.DayOfWeek = today.DayOfWeek with
        | true ->
            match stopDateTime with
            | None -> handleStart ()
            | Some stopDateTime -> stopDateTime |> handleStop
        | false ->
            match schedule.Recursively with
            | false ->
                match
                    match startDate > tomorrow with
                    | true -> startDate |> toNearestWorkday
                    | false -> tomorrow |> toNearestWorkday
                with
                | None -> Stopped(NotWorkday today.DayOfWeek)
                | Some nextDate -> StartIn(nextDate |> toStartDateTime |> toDelay, schedule)
            | true -> StartIn(nearestWorkday |> toStartDateTime |> toDelay, schedule)

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
