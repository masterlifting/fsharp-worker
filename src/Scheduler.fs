module internal Worker.Scheduler

open System
open Infrastructure.Prelude
open Worker.Domain

let rec private findWorkday (date: DateOnly) workdays =
    match workdays = Set.empty with
    | true -> date
    | false ->
        match workdays.Contains(date.DayOfWeek) with
        | true -> date
        | false -> workdays |> findWorkday (date.AddDays 1)

let private compute (now: DateTime) schedule =

    let today = DateOnly.FromDateTime now

    let toDelay dateTime = dateTime - now
    let toWorkday date = schedule.Workdays |> findWorkday date

    let startDate = schedule.StartDate |> Option.defaultValue today |> toWorkday
    let startTime = schedule.StartTime |> Option.defaultValue TimeOnly.MinValue
    let startDateTime = startDate.ToDateTime startTime

    let stopDateTime =
        match schedule.StopDate, schedule.StopTime with
        | Some stopDate, Some stopTime -> stopDate.ToDateTime stopTime |> Some
        | Some stopDate, None -> stopDate.ToDateTime TimeOnly.MinValue |> Some
        | None, Some stopTime -> today.ToDateTime stopTime |> Some
        | None, None -> None

    let nextWorkday = startDate.AddDays 1 |> toWorkday
    let nextStartDateTime = nextWorkday.ToDateTime startTime

    let nextStopDateTime =
        nextWorkday.ToDateTime(schedule.StopTime |> Option.defaultValue TimeOnly.MinValue)

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
                    match startDateTime < nextStopDateTime with
                    | true ->
                        match nextStartDateTime > now with
                        | true ->
                            match startDateTime < nextStartDateTime with
                            | true ->
                                match startDateTime > now with
                                | true -> StartIn(startDateTime |> toDelay, schedule)
                                | false -> StartIn(nextStopDateTime |> toDelay, schedule)
                            | false -> StartIn(nextStartDateTime |> toDelay, schedule)
                        | false -> Started schedule
                    | false ->
                        match nextStartDateTime > nextStopDateTime with
                        | true -> Stopped(StartTimeCannotBeReached nextStartDateTime)
                        | false -> StartIn(nextStartDateTime |> toDelay, schedule)

    match stopDateTime with
    | None -> handleStart ()
    | Some stopDateTime -> stopDateTime |> handleStop

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
