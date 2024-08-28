module internal Worker.Scheduler

open System
open System.Threading
open Infrastructure
open Infrastructure.Logging
open Worker.Domain

let private now (timeShift: int8) =
    DateTime.UtcNow.AddHours(timeShift |> float)

let private checkWorkday (taskName, recursively, schedule, (cts: CancellationTokenSource)) =
    async {
        let now = now schedule.TimeShift

        if now.DayOfWeek |> Set.contains >> not <| schedule.Workdays then
            let message = $"%s{taskName} Today is not a working day."

            match recursively with
            | None ->
                message |> Log.warning
                $"%s{taskName} Today is not an allowed day." |> Log.warning
                do! cts.CancelAsync() |> Async.AwaitTask
            | Some _ ->
                let delay = now.Date.AddDays 1. - now
                $"%s{message} Will be continued in %s{fromTimeSpan delay}." |> Log.warning
                do! Async.Sleep delay
    }

let private tryStopWork (taskName, recursively, schedule, (cts: CancellationTokenSource)) =
    async {
        let now = now schedule.TimeShift

        match schedule.StopDate with
        | Some stopDate ->
            let stopTime = schedule.StopTime |> Option.defaultValue TimeOnly.MaxValue
            let stopDateTime = stopDate.ToDateTime(stopTime)
            let delay = stopDateTime - now

            if delay > TimeSpan.Zero then

                if delay < TimeSpan.FromMinutes 10. then
                    $"%s{taskName} Will be stopped after %s{fromDateTime stopDateTime}."
                    |> Log.warning

                cts.CancelAfter delay
            else
                $"%s{taskName} Reached stop time %s{fromDateTime stopDateTime}." |> Log.warning
                do! cts.CancelAsync() |> Async.AwaitTask
        | None ->
            match schedule.StopTime with
            | Some stopTime ->
                let stopDateTime = DateTime.Today.Add(stopTime.ToTimeSpan())
                let delay = stopDateTime - now
                let message = $"%s{taskName} Reached stop time %s{fromDateTime stopDateTime}."

                match recursively with
                | Some _ ->
                    if delay > TimeSpan.Zero then

                        if delay < TimeSpan.FromMinutes 10. then
                            $"%s{taskName} Will be paused after %s{fromDateTime stopDateTime}."
                            |> Log.warning
                    else
                        let delay = now.Date.AddDays 1. - now
                        $"%s{message} Will be continued in %s{fromTimeSpan delay}." |> Log.warning
                        do! Async.Sleep delay
                | None ->
                    if delay > TimeSpan.Zero then
                        cts.CancelAfter delay
                    else
                        message |> Log.warning
                        do! cts.CancelAsync() |> Async.AwaitTask
            | None -> ()
    }

let private tryStartWork (taskName, schedule) =
    async {
        let now = now schedule.TimeShift
        let startDateTime = schedule.StartDate.ToDateTime(schedule.StartTime)

        match startDateTime - now with
        | delay when delay > TimeSpan.Zero ->
            $"%s{taskName} Will be started after %s{fromDateTime startDateTime}."
            |> Log.warning

            do! Async.Sleep delay
        | _ -> ()
    }

let getExpirationToken schedule recursively (cts: CancellationTokenSource) taskName =
    async {
        match schedule with
        | None -> return cts.Token
        | Some schedule ->

            if cts.Token |> notCanceled then
                do! checkWorkday (taskName, recursively, schedule, cts)

            if cts.Token |> notCanceled then
                do! tryStopWork (taskName, recursively, schedule, cts)

            if cts.Token |> notCanceled then
                do! tryStartWork (taskName, schedule)

            return cts.Token
    }
