module internal Worker.Scheduler

open System
open Worker.Domain

let private now (timeShift: int8) =
    DateTime.UtcNow.AddHours(timeShift |> float)

let private checkWorkday recursively schedule =
    let now = now schedule.TimeShift

    if now.DayOfWeek |> Set.contains >> not <| schedule.Workdays then
        if recursively then
            let delay = now.Date.AddDays 1. - now
            StartIn(delay, Some schedule)
        else
            Stopped(NotWorkday now.DayOfWeek, Some schedule)
    else
        Started(Some schedule)

let private tryStopWork recursively schedule =
    let now = now schedule.TimeShift

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

let private tryStartWork schedule =
    let now = now schedule.TimeShift
    let startDateTime = schedule.StartDate.ToDateTime(schedule.StartTime)

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
        { parent with
            Workdays = parent.Workdays |> Set.union child.Workdays }
        |> Some

let set parentSchedule schedule recursively =
    match parentSchedule |> merge <| schedule with
    | None -> Started None
    | Some schedule ->
        [ schedule |> checkWorkday recursively
          schedule |> tryStopWork recursively
          schedule |> tryStartWork ]
        |> List.minBy (function
            | Stopped _ -> 0
            | StartIn _ -> 1
            | StopIn _ -> 2
            | Started _ -> 3)
