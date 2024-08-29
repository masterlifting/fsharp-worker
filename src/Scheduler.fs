module internal Worker.Scheduler

open System
open Worker.Domain

let private now (timeShift: int8) =
    DateTime.UtcNow.AddHours(timeShift |> float)

let private utc (timeShift: int8) (dateTime: DateTime) =
    dateTime.AddHours(-(timeShift |> float))

let private checkWorkday (schedule, recursively) task =
    let now = now schedule.TimeShift

    if now.DayOfWeek |> Set.contains >> not <| schedule.Workdays then
        match recursively with
        | None -> Scheduler.Stop <| NotWorkday
        | Some _ ->
            let delay = now.Date.AddDays 1. - now
            Scheduler.Wait delay
    else
        Scheduler.Continue

let private tryStopWork (schedule, recursively) task =
    let now = now schedule.TimeShift

    match schedule.StopDate with
    | Some stopDate ->
        let stopTime = schedule.StopTime |> Option.defaultValue TimeOnly.MaxValue
        let stopDateTime = stopDate.ToDateTime(stopTime)
        let delay = stopDateTime - now

        if delay > TimeSpan.Zero then
            let utcStopDateTime = stopDateTime |> utc schedule.TimeShift
            Scheduler.StopAfter utcStopDateTime
        else
            Scheduler.Stop <| StopDateReached
    | None ->
        match schedule.StopTime with
        | Some stopTime ->
            let stopDateTime = DateTime.Today.Add(stopTime.ToTimeSpan())
            let delay = stopDateTime - now

            if delay > TimeSpan.Zero then
                let utcStopDateTime = stopDateTime |> utc schedule.TimeShift
                Scheduler.StopAfter utcStopDateTime
            else
                match recursively with
                | Some _ ->
                    let delay = now.Date.AddDays 1. - now
                    Scheduler.Wait delay
                | None -> Scheduler.Stop <| StopTimeReached
        | None -> Scheduler.Continue

let private tryStartWork schedule task =
    let now = now schedule.TimeShift
    let startDateTime = schedule.StartDate.ToDateTime(schedule.StartTime)
    let delay = startDateTime - now

    if delay > TimeSpan.Zero then
        Scheduler.Wait delay
    else
        Scheduler.Start

let set scheduler schedule recursively task =
    let schedulers =
        match schedule with
        | None -> [ Scheduler.Continue ]
        | Some schedule ->

            let workdayScheduler = task |> checkWorkday (schedule, recursively)
            let stopWorkScheduler = task |> tryStopWork (schedule, recursively)
            let startWorkScheduler = task |> tryStartWork schedule

            [ workdayScheduler; stopWorkScheduler; startWorkScheduler ]
        |> List.append [ scheduler ]

    if schedulers |> List.contains Scheduler.Stop then
        Scheduler.Stop
    else
        let woc =
            match
                schedulers
                |> List.map (function
                    | Scheduler.Wait delay -> Some delay
                    | _ -> None)
                |> List.choose id
            with
            | [] -> Scheduler.Continue
            | delays -> delays |> List.max |> Scheduler.Wait

        let sac =
            match
                schedulers
                |> List.map (function
                    | Scheduler.StopAfter dateTime -> Some dateTime
                    | _ -> None)
                |> List.choose id
            with
            | [] -> Scheduler.Continue
            | stopTimes ->
                let stopTime = stopTimes |> List.min
                let now = now 0y
                let delay = stopTime - now

                if (delay > TimeSpan.Zero) then
                    Scheduler.StopAfter stopTime
                else
                    Scheduler.Stop

        match woc, sac with
        | _, Scheduler.Stop -> Scheduler.Stop
        | Scheduler.Continue, Scheduler.StopAfter stopTime ->
            let utcNow = now 0y

            if stopTime > utcNow then
                Scheduler.StopAfter stopTime
            else
                match recursively with
                | Some _ ->
                    let nextDay = utcNow.Date.AddDays 1.
                    Scheduler.Wait(nextDay - utcNow)
                | None -> Scheduler.Stop

        | Scheduler.Wait delay, Scheduler.Continue -> Scheduler.Wait delay
        | Scheduler.Wait delay, Scheduler.StopAfter stopTime ->
            let utcNow = now 0y

            if delay < stopTime - utcNow then
                Scheduler.Wait delay
            else if stopTime > utcNow then
                Scheduler.StopAfter stopTime
            else
                match recursively with
                | Some _ ->
                    let nextDay = utcNow.Date.AddDays 1.
                    Scheduler.Wait(nextDay - utcNow)
                | None -> Scheduler.Stop
        | _, _ -> Scheduler.Continue
