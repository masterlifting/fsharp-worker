module internal Worker.Scheduler

open System
open Worker.Domain

let private checkWorkday recursively (now: DateTime) schedule =

    if now.DayOfWeek |> Set.contains >> not <| schedule.Workdays then
        if recursively then
            let delay = now.Date.AddDays 1. - now
            StartIn(delay, Some schedule)
        else
            Stopped(NotWorkday now.DayOfWeek, Some schedule)
    else
        Started(Some schedule)

let private tryStopWork recursively (now: DateTime) schedule =

    match schedule.StopDate with
    | Some stopDate ->
        let stopTime = schedule.StopTime |> Option.defaultValue TimeOnly.MaxValue
        let stopDateTime = stopDate.ToDateTime stopTime

        if stopDateTime >= now then
            let delay = stopDateTime - now
            StopIn(delay, Some schedule)
        else
            Stopped(StopDateReached stopDate, Some schedule)
    | None ->
        match schedule.StopTime with
        | Some stopTime ->
            let stopDateTime = now.Date.Add(stopTime.ToTimeSpan())

            if stopDateTime >= now then
                let delay = stopDateTime - now
                StopIn(delay, Some schedule)
            else if recursively then
                let delay = now.Date.AddDays 1. - now
                StartIn(delay, Some schedule)
            else
                Stopped(StopTimeReached stopTime, Some schedule)
        | None -> Started(Some schedule)

let private tryStartWork (now: DateTime) schedule =
    let startDateTime =
        match schedule.StartDate, schedule.StartTime with
        | Some startDate, Some startTime -> startDate.ToDateTime startTime
        | Some startDate, None -> startDate.ToDateTime TimeOnly.MinValue
        | None, Some startTime -> now.Date.Add(startTime.ToTimeSpan())
        | None, None -> now

    if startDateTime > now then
        let delay = startDateTime - now
        StartIn(delay, Some schedule)
    else
        Started(Some schedule)

let private merge parent child =
    match parent, child with
    | None, None -> None
    | Some parent, None -> Some parent
    | None, Some child -> Some child
    | Some parent, Some child ->
        let workdays = parent.Workdays |> Set.union child.Workdays

        let startDate =
            if child.StartDate > parent.StartDate then
                child.StartDate
            else
                parent.StartDate

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

        { parent with
            Workdays = workdays
            StartDate = startDate
            StopDate = stopDate }
        |> Some

let set parentSchedule schedule recursively =
    match parentSchedule |> merge <| schedule with
    | None -> Started None
    | Some schedule ->
        let now = DateTime.UtcNow.AddHours(schedule.TimeZone |> float)

        [ schedule |> checkWorkday recursively now
          schedule |> tryStopWork recursively now
          schedule |> tryStartWork now ]
        |> List.minBy (function
            | Stopped _ -> 0
            | StartIn _ -> 1
            | StopIn _ -> 2
            | Started _ -> 3)
