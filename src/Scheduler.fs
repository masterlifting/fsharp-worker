module internal Worker.Scheduler

open System
open System.Threading
open Domain.Core
open Infrastructure.Logging

let getExpirationToken task schedule =
    async {
        let cts = new CancellationTokenSource()

        match schedule with
        | None -> return cts.Token
        | Some schedule ->
            let now = DateTime.UtcNow.AddHours(schedule.TimeShift |> float)
            
            if not schedule.IsEnabled then
                $"Task '%s{task}' is disabled" |> Log.warning
                do! cts.CancelAsync() |> Async.AwaitTask

            if not cts.IsCancellationRequested then
                match schedule.StopWork with
                | Some stopWork ->
                    match stopWork - now with
                    | delay when delay > TimeSpan.Zero ->
                        $"Task '%s{task}' will be stopped at {stopWork}" |> Log.warning
                        cts.CancelAfter delay
                    | _ -> do! cts.CancelAsync() |> Async.AwaitTask
                | _ -> ()

            if not cts.IsCancellationRequested then
                match schedule.StartWork - now with
                | delay when delay > TimeSpan.Zero ->
                    $"Task '%s{task}' will start at {schedule.StartWork}" |> Log.warning
                    do! Async.Sleep delay
                | _ -> ()

                if schedule.IsOnce then
                    $"Task '%s{task}' will be run once" |> Log.warning
                    cts.CancelAfter(schedule.Delay.Subtract(TimeSpan.FromSeconds 1.0))

            return cts.Token
    }
