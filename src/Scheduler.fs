module TaskScheduler

open System
open System.Threading
open Domain.Worker
open Infrastructure.Logging

let getTaskExpirationToken taskName scheduler =
    async {
        let now = DateTime.UtcNow.AddHours(scheduler.TimeShift)
        let cts = new CancellationTokenSource()

        if not scheduler.IsEnabled then
            $"Task '{taskName}' is disabled" |> Logger.logWarning
            do! cts.CancelAsync() |> Async.AwaitTask

        if
            not cts.IsCancellationRequested
            && scheduler.WorkDays |> Seq.contains (now.DayOfWeek) |> not
        then
            $"Task '{taskName}' is not scheduled for today" |> Logger.logWarning
            do! cts.CancelAsync() |> Async.AwaitTask

        if not cts.IsCancellationRequested && scheduler.StopWork.IsSome then
            match scheduler.StopWork.Value - now with
            | ts when ts > TimeSpan.Zero ->
                $"Task '{taskName}' will be stopped at {scheduler.StopWork.Value}"
                |> Logger.logWarning

                cts.CancelAfter ts
            | _ -> do! cts.CancelAsync() |> Async.AwaitTask

        if not cts.IsCancellationRequested && scheduler.StartWork.IsSome then
            match scheduler.StartWork.Value - now with
            | ts when ts > TimeSpan.Zero ->
                $"Task '{taskName}' will start at {scheduler.StartWork.Value}"
                |> Logger.logWarning

                do! Async.Sleep ts
            | _ -> ()

            if scheduler.IsOnce then
                $"Task '{taskName}' will be run once" |> Logger.logWarning
                cts.CancelAfter(scheduler.Delay.Subtract(TimeSpan.FromSeconds 1.0))

        return cts.Token
    }
