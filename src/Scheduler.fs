module internal Worker.Scheduler

open System
open Worker.Domain

let rec private findNearestWorkday (date: DateOnly) workdays =
    match workdays = Set.empty with
    | true -> None
    | false ->
        match workdays.Contains(date.DayOfWeek) with
        | true -> Some date
        | false -> workdays |> findNearestWorkday (date.AddDays 1)

let private checkWorkday (now: DateTime) schedule =
    let today = now |> DateOnly.FromDateTime

    match schedule.Workdays |> findNearestWorkday today with
    | None -> Started schedule
    | Some nearestWorkday ->
        match nearestWorkday.DayOfWeek = today.DayOfWeek with
        | true -> Started schedule
        | false ->
            match schedule.Continue with
            | false -> Stopped(NotWorkday today.DayOfWeek)
            | true ->
                let startDateTime =
                    match schedule.StartTime with
                    | Some startTime -> nearestWorkday.ToDateTime startTime
                    | None -> nearestWorkday.ToDateTime TimeOnly.MinValue

                let delay = startDateTime - now
                StartIn(delay, schedule)

let private tryStopWork (now: DateTime) schedule =
    let stopDateTime =
        match schedule.StopDate, schedule.StopTime with
        | Some stopDate, Some stopTime -> stopDate.ToDateTime stopTime |> Some
        | Some stopDate, None -> stopDate.ToDateTime TimeOnly.MinValue |> Some
        | None, Some stopTime -> now.Date.Add(stopTime.ToTimeSpan()) |> Some
        | None, None -> None

    match stopDateTime with
    | None -> Started schedule
    | Some stopDateTime ->
        match stopDateTime > now with
        | true ->
            let delay = stopDateTime - now
            StopIn(delay, schedule)
        | false ->
            match schedule.Continue with
            | false -> Stopped(StopTimeReached stopDateTime)
            | true ->
                let tomorrow = now.Date.AddDays 1. |> DateOnly.FromDateTime

                let nextDate =
                    match schedule.Workdays |> findNearestWorkday tomorrow with
                    | Some nearestWorkday -> nearestWorkday
                    | None -> tomorrow

                let startDateTime =
                    match schedule.StartTime with
                    | Some startTime -> nextDate.ToDateTime startTime
                    | None -> nextDate.ToDateTime TimeOnly.MinValue

                let delay = startDateTime - now
                StartIn(delay, schedule)

let private tryStartWork (now: DateTime) schedule =
    let startDateTime =
        match schedule.StartDate, schedule.StartTime with
        | Some startDate, Some startTime -> startDate.ToDateTime startTime
        | Some startDate, None -> startDate.ToDateTime TimeOnly.MinValue
        | None, Some startTime -> now.Date.Add(startTime.ToTimeSpan())
        | None, None -> now

    match startDateTime > now with
    | true ->
        let delay = startDateTime - now
        StartIn(delay, schedule)
    | false -> Started schedule

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
        |> List.groupBy id
        |> List.map (function
            | Stopped _, values -> values |> List.head
            | StartIn _, values ->
                values
                |> List.maxBy (function
                    | StartIn(delay, _) -> delay
                    | _ -> TimeSpan.MinValue)
            | StopIn _, values ->
                values
                |> List.minBy (function
                    | StopIn(delay, _) -> delay
                    | _ -> TimeSpan.MaxValue)
            | Started _, values -> values |> List.head
            | NotScheduled, values -> values |> List.head)
        |> List.minBy (function
            | Stopped _ -> 0
            | StartIn _ -> 1
            | StopIn _ -> 2
            | Started _ -> 3
            | NotScheduled -> 4)
