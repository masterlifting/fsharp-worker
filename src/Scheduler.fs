module internal Worker.Scheduler

open System
open Infrastructure
open Infrastructure.Logging
open Worker.Domain

let private now (timeShift: int8) =
    DateTime.UtcNow.AddHours(timeShift |> float)

let private checkWorkday recursively schedule =
    let now = now schedule.TimeShift

    if now.DayOfWeek |> Set.contains >> not <| schedule.Workdays then
        match recursively with
        | None -> Expired NotWorkday
        | Some _ -> ReadyAfter <| now.Date.AddDays 1.
    else
        Ready

let private tryStopWork recursively schedule =
    let now = now schedule.TimeShift

    match schedule.StopDate with
    | Some stopDate ->
        let stopTime = schedule.StopTime |> Option.defaultValue TimeOnly.MaxValue
        let stopDateTime = stopDate.ToDateTime stopTime

        if stopDateTime >= now then
            ExpiredAfter stopDateTime
        else
            Expired StopDateReached
    | None ->
        match schedule.StopTime with
        | Some stopTime ->
            let stopDateTime = now.Date.Add(stopTime.ToTimeSpan())

            if stopDateTime >= now then
                ExpiredAfter stopDateTime
            else
                match recursively with
                | Some _ -> ReadyAfter <| now.Date.AddDays 1.
                | None -> Expired StopTimeReached
        | None -> Ready

let private tryStartWork schedule =
    let now = now schedule.TimeShift
    let startDateTime = schedule.StartDate.ToDateTime(schedule.StartTime)

    if startDateTime > now then
        ReadyAfter startDateTime
    else
        Ready

let private merge parent child =
    match parent, child with
    | Some parent, None -> Some parent
    | None, Some child -> Some child
    | None, None -> None
    | Some parent, Some child -> Some parent

let private processScheduler schedule taskName scheduler =
    async {
        let now = now schedule.TimeShift

        match scheduler with
        | Expired reason -> return Error reason.Message
        | Ready -> return Ok()
        | ReadyAfter startDateTime ->
            if startDateTime <= now then
                return Ok()
            else
                $"%s{taskName} Will start after {fromDateTime startDateTime}." |> Log.warning
                do! Async.Sleep(startDateTime - now)
                return Ok()
        | ExpiredAfter stopDateTime ->
            $"%s{taskName} Will stop after {fromDateTime stopDateTime}." |> Log.warning
            return Ok()
    }

let set parentSchedule schedule recursively taskName =
    async {
        match parentSchedule |> merge <| schedule with
        | None -> return (None, Ok())
        | Some schedule ->
            let! validationResult =
                resultAsync {
                    let! _ = schedule |> checkWorkday recursively |> processScheduler schedule taskName
                    let! _ = schedule |> tryStopWork recursively |> processScheduler schedule taskName
                    return schedule |> tryStartWork |> processScheduler schedule taskName
                }

            match validationResult with
            | Ok _ -> return (Some schedule, Ok())
            | Error msg -> return (Some schedule, Error msg)
    }
