module internal Worker.Scheduler

open System
open Infrastructure.Prelude
open Worker.Domain

let private compute (now: DateTime) (schedule: Schedule) =

    let today = DateOnly.FromDateTime now

    let toDelay dateTime = dateTime - now

    let rec toNearestWorkday (date: DateOnly) =
        match schedule.Workdays.Contains(date.DayOfWeek) with
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

    let toNextStartDateTime () =
        let nextStartDate = startDate.AddDays 1 |> toNearestWorkday
        nextStartDate.ToDateTime startTime

    let toNextStopDateTime (stopDateTime: DateTime) =
        let stopDate = DateOnly.FromDateTime stopDateTime
        let stopTime = TimeOnly.FromDateTime stopDateTime
        let nextStopDate = stopDate.AddDays 1 |> toNearestWorkday
        nextStopDate.ToDateTime stopTime

    let rec handleContinuation startDateTime stopDateTime =
        let started =
            startDateTime < stopDateTime && startDateTime <= now && stopDateTime > now

        let startIn =
            (startDateTime < stopDateTime && startDateTime > now)
            || (startDateTime > stopDateTime && startDateTime > now)

        match started, startIn with
        | true, _ -> Started schedule
        | false, true -> StartIn(startDateTime |> toDelay, schedule)
        | false, false ->
            let nextStartDateTime =
                match startDateTime > stopDateTime with
                | true -> startDateTime
                | false -> toNextStartDateTime ()

            let nextStopDateTime = toNextStopDateTime stopDateTime
            handleContinuation nextStartDateTime nextStopDateTime

    match schedule.Workdays.IsEmpty with
    | true -> Stopped(EmptyWorkdays)
    | false ->
        match stopDateTime with
        | None ->
            match startDateTime > now with
            | true -> StartIn(startDateTime |> toDelay, schedule)
            | false -> Started schedule
        | Some stopDateTime ->
            match schedule.StopDate.IsSome with
            | true ->
                match startDateTime > now with
                | true ->
                    match startDateTime > stopDateTime with
                    | true -> Stopped(StartTimeCannotBeReached startDateTime)
                    | false ->
                        match startDateTime > now with
                        | true ->
                             let nextStartDate = startDate |> toNearestWorkday
                             let nextStartDateTime =   nextStartDate.ToDateTime startTime
                             StartIn(nextStartDateTime |> toDelay, schedule)
                        | false -> StopIn(stopDateTime |> toDelay, schedule)
                | false ->
                    match startDateTime > stopDateTime with
                    | true -> Stopped(StartTimeCannotBeReached startDateTime)
                    | false ->
                        match stopDateTime <= now with
                        | true -> Stopped(StopTimeReached stopDateTime)
                        | false -> StopIn(stopDateTime |> toDelay, schedule)
            | false ->
                match schedule.Recursively with
                | false -> Stopped(StopTimeReached stopDateTime)
                | true -> handleContinuation startDateTime stopDateTime

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
