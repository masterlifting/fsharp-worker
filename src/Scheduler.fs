module internal Worker.Scheduler

open System
open System.Threading
open Domain.Core
open Infrastructure.Logging

let getExpirationToken task schedule =
    async {
        let cts = new CancellationTokenSource()

        match schedule with
        | None ->
            $"Task '%s{task}'. Schedule is disabled." |> Log.warning 
            return cts.Token
        | Some schedule ->
            let now = DateTime.UtcNow.AddHours(schedule.TimeShift |> float)
            
            if not cts.IsCancellationRequested then
                match schedule.StopWork with
                | Some stopWork ->
                    match stopWork - now with
                    | delay when delay > TimeSpan.Zero ->
                        $"Task '%s{task}'. Will be disabled at {stopWork}." |> Log.warning
                        cts.CancelAfter delay
                    | _ -> do! cts.CancelAsync() |> Async.AwaitTask
                | _ -> ()

            if not cts.IsCancellationRequested then
                match schedule.StartWork - now with
                | delay when delay > TimeSpan.Zero ->
                    $"Task '%s{task}'. Will ready at {schedule.StartWork}." |> Log.warning
                    do! Async.Sleep delay
                | _ -> ()

            return cts.Token
    }
