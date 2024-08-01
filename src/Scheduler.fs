module internal Worker.Scheduler

open System
open System.Threading
open Infrastructure
open Infrastructure.Logging
open Worker.Domain

let private now timeShift =
    DateTime.UtcNow.AddHours(timeShift |> float)

let private checkWorkday (cts: CancellationTokenSource) (task: Task) timeShift (workdays: Set<DayOfWeek>) =
    async {
        let now = now timeShift

        if now.DayOfWeek |> Set.contains >> not <| workdays then
            let message = $"Task '%s{task.Name}'. Today is not a working day."

            if not task.Recursively then
                message |> Log.warning
                do! cts.CancelAsync() |> Async.AwaitTask
            else
                let delay = now.Date.AddDays(1.) - now
                $"{message} Will be started in {delay}." |> Log.warning
                do! Async.Sleep delay
    }

let private checkLimit (cts: CancellationTokenSource) (task: Task) timeShift (limit: uint option) count =
    async {
        match limit with
        | Some limit when count % (limit + 1u) = 0u ->
            let message = $"Task '%s{task.Name}'. Limit {limit} reached"

            if not task.Recursively then
                $"{message}." |> Log.warning
                do! cts.CancelAsync() |> Async.AwaitTask
            else
                let now = now timeShift
                let delay = now.Date.AddDays(1.) - now
                let formattedDelay = delay.ToString("dd\\d\\ hh\\h\\ mm\\m\\ ss\\s")

                $"{message} for today. New limit will be available in {formattedDelay}."
                |> Log.warning

                do! Async.Sleep delay
        | _ -> ()
    }

let private tryStopWork (cts: CancellationTokenSource) (task: Task) timeShift (stopWork: DateTime option) =
    async {
        match stopWork with
        | Some stopWork ->
            match stopWork - now timeShift with
            | delay when delay > TimeSpan.Zero ->
                $"Task '%s{task.Name}'. Will be stopped at {stopWork}." |> Log.warning
                cts.CancelAfter delay
            | _ -> do! cts.CancelAsync() |> Async.AwaitTask
        | _ -> ()
    }

let private tryStartWork (task: Task) timeShift (startWork: DateTime) =
    async {
        match startWork - now timeShift with
        | delay when delay > TimeSpan.Zero ->
            $"Task '%s{task.Name}'. Will be started at {startWork}." |> Log.warning
            do! Async.Sleep delay
        | _ -> ()
    }

let getExpirationToken task count (cts: CancellationTokenSource) =
    async {
        match task.Schedule with
        | None -> return cts.Token
        | Some schedule ->

            do! checkLimit cts task schedule.TimeShift schedule.Limit count

            if cts.Token |> notCanceled then
                do! tryStopWork cts task schedule.TimeShift schedule.StopWork

            if cts.Token |> notCanceled then
                do! checkWorkday cts task schedule.TimeShift schedule.Workdays

            if cts.Token |> notCanceled then
                do! tryStartWork task schedule.TimeShift schedule.StartWork

            return cts.Token
    }
