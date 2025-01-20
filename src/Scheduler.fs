module internal Worker.Scheduler

open System
open Worker.Domain

let private checkWorkday (now: DateTime) schedule =

    if now.DayOfWeek |> Set.contains >> not <| schedule.Workdays then
        if schedule.Continue then
            let delay = now.Date.AddDays 1. - now
            StartIn(delay, schedule)
        else
            Stopped(NotWorkday now.DayOfWeek, schedule)
    else
        Started schedule

let private tryStopWork (now: DateTime) schedule =

    match schedule.StopDate with
    | Some stopDate ->
        let stopTime = schedule.StopTime |> Option.defaultValue TimeOnly.MaxValue
        let stopDateTime = stopDate.ToDateTime stopTime

        if stopDateTime >= now then
            let delay = stopDateTime - now
            StopIn(delay, schedule)
        else
            Stopped(StopDateReached stopDate, schedule)
    | None ->
        match schedule.StopTime with
        | Some stopTime ->
            let stopDateTime = now.Date.Add(stopTime.ToTimeSpan())

            if stopDateTime >= now then
                let delay = stopDateTime - now
                StopIn(delay, schedule)
            else if schedule.Continue then
                let delay = now.Date.AddDays 1. - now
                StartIn(delay, schedule)
            else
                Stopped(StopTimeReached stopTime, schedule)
        | None -> Started schedule

let private tryStartWork (now: DateTime) schedule =
    let startDateTime =
        match schedule.StartDate, schedule.StartTime with
        | Some startDate, Some startTime -> startDate.ToDateTime startTime
        | Some startDate, None -> startDate.ToDateTime TimeOnly.MinValue
        | None, Some startTime -> now.Date.Add(startTime.ToTimeSpan())
        | None, None -> now

    if startDateTime > now then
        let delay = startDateTime - now
        StartIn(delay, schedule)
    else
        Started schedule

let private merge parent current =
    fun withContinue ->
        match parent, current with
        | None, None -> None
        | Some parent, None -> Some parent
        | None, Some current -> Some current
        | Some parent, Some current ->
            let workdays = parent.Workdays |> Set.intersect current.Workdays

            let startDate =
                match parent.StartDate, current.StartDate with
                | Some parentStartDate, Some childStartDate -> Some(max parentStartDate childStartDate)
                | Some parentStartDate, None -> Some parentStartDate
                | None, Some currentStartDate -> Some currentStartDate
                | None, None -> None

            let stopDate =
                match parent.StopDate, current.StopDate with
                | Some parentStopDate, Some currentStopDate -> Some(min parentStopDate currentStopDate)
                | Some parentStopDate, None -> Some parentStopDate
                | None, Some currentStopDate -> Some currentStopDate
                | None, None -> None

            let startTime =
                match parent.StartTime, current.StartTime with
                | Some parentStartTime, Some currentStartTime -> Some(max parentStartTime currentStartTime)
                | Some parentStartTime, None -> Some parentStartTime
                | None, Some currentStartTime -> Some currentStartTime
                | None, None -> None

            let stopTime =
                match parent.StopTime, current.StopTime with
                | Some parentStopTime, Some currentStopTime -> Some(min parentStopTime currentStopTime)
                | Some parentStopTime, None -> Some parentStopTime
                | None, Some currentStopTime -> Some currentStopTime
                | None, None -> None

            { parent with
                Workdays = workdays
                StartDate = startDate
                StopDate = stopDate
                StartTime = startTime
                StopTime = stopTime
                Continue = parent.Continue || current.Continue
                TimeZone = current.TimeZone }
            |> Some
        |> Option.map (fun schedule ->
            { schedule with
                Continue = withContinue || schedule.Continue })

let set parentSchedule schedule withContinue =
    let mergeSchedules = parentSchedule |> merge <| schedule

    match mergeSchedules withContinue with
    | None -> NotScheduled
    | Some schedule ->
        let now = DateTime.UtcNow.AddHours(schedule.TimeZone |> float)

        [ schedule |> checkWorkday now
          schedule |> tryStopWork now
          schedule |> tryStartWork now ]
        |> List.minBy (function
            | Stopped _ -> 0
            | StartIn _ -> 1
            | StopIn _ -> 2
            | Started _ -> 3
            | NotScheduled -> 4)
