module internal Worker.Scheduler

open System
open System.Threading
open Infrastructure
open Infrastructure.Logging
open Worker.Domain

let private now (timeShift: int8) =
    DateTime.UtcNow.AddHours(timeShift |> float)

let private checkWorkday
    (taskName, recursively, timeShift, workdays, (cts: CancellationTokenSource))
    =
    async {
        let now = now timeShift

        if now.DayOfWeek |> Set.contains >> not <| workdays then
            let message = $"%s{taskName} Today is not a working day."

            match recursively with
            | None ->
                message |> Log.warning
                do! cts.CancelAsync() |> Async.AwaitTask
            | Some _ ->
                let delay = now.Date.AddDays 1. - now
                
                $"{message} Will be started in {delay}." |> Log.warning
                
                do! Async.Sleep delay
    }

let private tryStopWork
    (taskName, timeShift, stopWork,(cts: CancellationTokenSource))
    =
    async {
        match stopWork with
        | Some stopWork ->
            match stopWork - now timeShift with
            | delay when delay > TimeSpan.Zero ->
                
                if delay < TimeSpan.FromHours 1. then
                    $"%s{taskName} Will be stopped at {stopWork}." |> Log.warning
                
                cts.CancelAfter delay
            | _ -> do! cts.CancelAsync() |> Async.AwaitTask
        | _ -> ()
    }

let private tryStartWork (taskName, timeShift, startWork) =
    async {
        match startWork - now timeShift with
        | delay when delay > TimeSpan.Zero ->
            
            $"%s{taskName} Will be started at {startWork}." |> Log.warning

            do! Async.Sleep delay
        | _ -> ()
    }

let getExpirationToken schedule recursively (cts: CancellationTokenSource) taskName=
    async {
        match schedule with
        | None -> return cts.Token
        | Some schedule ->

            if cts.Token |> notCanceled then
                do! tryStopWork (taskName, schedule.TimeShift, schedule.StopWork, cts)

            if cts.Token |> notCanceled then
                do! checkWorkday (taskName, recursively, schedule.TimeShift, schedule.Workdays, cts)

            if cts.Token |> notCanceled then
                do! tryStartWork (taskName, schedule.TimeShift, schedule.StartWork)

            return cts.Token
    }
