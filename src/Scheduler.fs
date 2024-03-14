module Scheduler

open System
open System.Threading
open Helpers
open Domain.Settings
open Infrastructure.Logging

let getTaskDelay schedule =
    match schedule.Delay with
    | IsTimeSpan value -> value
    | _ -> TimeSpan.Zero

let getTaskExpirationToken task (delay: TimeSpan) schedule =
    async {
        let now = DateTime.UtcNow.AddHours(float schedule.TimeShift)
        let cts = new CancellationTokenSource()

        if not schedule.IsEnabled then
            $"Task '{task}' is disabled" |> Logger.logWarning
            do! cts.CancelAsync() |> Async.AwaitTask

        if not cts.IsCancellationRequested then
            match schedule.StopWork with
            | HasValue stopWork ->
                match stopWork - now with
                | ts when ts > TimeSpan.Zero ->
                    $"Task '{task}' will be stopped at {stopWork}" |> Logger.logWarning
                    cts.CancelAfter ts
                | _ -> do! cts.CancelAsync() |> Async.AwaitTask
            | _ -> ()

        if not cts.IsCancellationRequested then
            match schedule.StartWork with
            | HasValue startWork ->
                match startWork - now with
                | ts when ts > TimeSpan.Zero ->
                    $"Task '{task}' will start at {startWork}" |> Logger.logWarning
                    do! Async.Sleep ts
                | _ -> ()
            | _ -> ()

            if schedule.IsOnce then
                $"Task '{task}' will be run once" |> Logger.logWarning
                cts.CancelAfter(delay.Subtract(TimeSpan.FromSeconds 1.0))

        return cts.Token
    }
