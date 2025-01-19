module internal Worker.Scheduler

open System
open Worker.Domain

let private checkWorkday recursively (now: DateTime) schedule =

    if now.DayOfWeek |> Set.contains >> not <| schedule.Workdays then
        if recursively then
            let delay = now.Date.AddDays 1. - now
            StartIn(delay, schedule)
        else
            Stopped(NotWorkday now.DayOfWeek, schedule)
    else
        Started schedule

let private tryStopWork recursively (now: DateTime) schedule =

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
            else if recursively then
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

let private merge parent child =
    match parent, child with
    | None, None -> None
    | Some parent, None -> Some parent
    | None, Some child -> Some child
    | Some parent, Some child ->
        let workdays = parent.Workdays |> Set.intersect child.Workdays

        let startDate =
            match parent.StartDate, child.StartDate with
            | Some parentStartDate, Some childStartDate ->
                Some(
                    if childStartDate > parentStartDate then
                        childStartDate
                    else
                        parentStartDate
                )
            | Some parentStartDate, None -> Some parentStartDate
            | None, Some childStartDate -> Some childStartDate
            | None, None -> None

        let stopDate =
            match parent.StopDate, child.StopDate with
            | Some parentStopDate, Some childStopDate ->
                Some(
                    if childStopDate < parentStopDate then
                        childStopDate
                    else
                        parentStopDate
                )
            | Some parentStopDate, None -> Some parentStopDate
            | None, Some childStopDate -> Some childStopDate
            | None, None -> None

        let startTime =
            match parent.StartTime, child.StartTime with
            | Some parentStartTime, Some childStartTime ->
                Some(
                    if childStartTime > parentStartTime then
                        childStartTime
                    else
                        parentStartTime
                )
            | Some parentStartTime, None -> Some parentStartTime
            | None, Some childStartTime -> Some childStartTime
            | None, None -> None

        let stopTime =
            match parent.StopTime, child.StopTime with
            | Some parentStopTime, Some childStopTime ->
                Some(
                    if childStopTime < parentStopTime then
                        childStopTime
                    else
                        parentStopTime
                )
            | Some parentStopTime, None -> Some parentStopTime
            | None, Some childStopTime -> Some childStopTime
            | None, None -> None

        { parent with
            Workdays = workdays
            StartDate = startDate
            StopDate = stopDate
            StartTime = startTime
            StopTime = stopTime
            TimeZone = child.TimeZone }
        |> Some

let set parentSchedule schedule recursively =
    match parentSchedule |> merge <| schedule with
    | None -> NotScheduled
    | Some schedule ->
        let now = DateTime.UtcNow.AddHours(schedule.TimeZone |> float)

        [ schedule |> checkWorkday recursively now
          schedule |> tryStopWork recursively now
          schedule |> tryStartWork now ]
        |> List.minBy (function
            | Stopped _ -> 0
            | StartIn _ -> 1
            | StopIn _ -> 2
            | Started _ -> 3
            | NotScheduled -> 4)
