module Scheduler

open System
open System.Threading
open Domain.Core

let internal getExpirationToken taskName scheduler =
    async {
        let now = DateTime.UtcNow.AddHours(scheduler.TimeShift |> float)
        let cts = new CancellationTokenSource()

        if not scheduler.IsEnabled then
            $"Task '%s{taskName}' is disabled" |> Log.warning
            do! cts.CancelAsync() |> Async.AwaitTask

        if not cts.IsCancellationRequested then
            match scheduler.StopWork with
            | Some stopWork ->
                match stopWork - now with
                | delay when delay > TimeSpan.Zero ->
                    $"Task '%s{taskName}' will be stopped at {stopWork}" |> Log.warning
                    cts.CancelAfter delay
                | _ -> do! cts.CancelAsync() |> Async.AwaitTask
            | _ -> ()

        if not cts.IsCancellationRequested then
            match scheduler.StartWork - now with
            | delay when delay > TimeSpan.Zero ->
                $"Task '%s{taskName}' will start at {scheduler.StartWork}" |> Log.warning
                do! Async.Sleep delay
            | _ -> ()

            if scheduler.IsOnce then
                $"Task '%s{taskName}' will be run once" |> Log.warning
                cts.CancelAfter(scheduler.Delay.Subtract(TimeSpan.FromSeconds 1.0))

        return cts.Token
    }
