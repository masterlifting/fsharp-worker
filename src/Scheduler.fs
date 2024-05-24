module internal Worker.Scheduler

open System
open System.Threading
open Domain.Core
open Infrastructure.Logging
open Infrastructure.DSL.Threading

let getExpirationToken task count (cts: CancellationTokenSource) =
    async {
        match task.Schedule with
        | None -> return cts.Token
        | Some schedule ->
            let now = DateTime.UtcNow.AddHours(schedule.TimeShift |> float)

            if
                cts.Token |> notCanceled
                && not (schedule.WorkDays |> Set.contains now.DayOfWeek)
            then
                $"Task '%s{task.Name}'. Today is not a working day." |> Log.warning

                if not task.Recursively then
                    do! cts.CancelAsync() |> Async.AwaitTask

            if cts.Token |> notCanceled then
                match schedule.Limit with
                | Some limit when count = limit ->
                    $"Task '%s{task.Name}'. Limit exceeded." |> Log.warning
                    do! cts.CancelAsync() |> Async.AwaitTask
                | _ -> ()

            if cts.Token |> notCanceled then
                match schedule.StopWork with
                | Some stopWork ->
                    match stopWork - now with
                    | delay when delay > TimeSpan.Zero ->
                        $"Task '%s{task.Name}'. Will be stopped at {stopWork}." |> Log.warning
                        cts.CancelAfter delay
                    | _ -> do! cts.CancelAsync() |> Async.AwaitTask
                | _ -> ()

            if cts.Token |> notCanceled then
                match schedule.StartWork - now with
                | delay when delay > TimeSpan.Zero ->
                    $"Task '%s{task.Name}'. Will be started at {schedule.StartWork}." |> Log.warning
                    do! Async.Sleep delay
                | _ -> ()

            return cts.Token
    }
