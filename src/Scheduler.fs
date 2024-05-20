module internal Worker.Scheduler

open System
open System.Threading
open Domain.Core
open Infrastructure.Logging
open Infrastructure.Domain.Graph

let getExpirationToken (task: INodeHandle) schedule (cts: CancellationTokenSource) =
    async {
        match schedule with
        | None -> return cts.Token
        | Some schedule ->
            let now = DateTime.UtcNow.AddHours(schedule.TimeShift |> float)

            if not (schedule.WorkDays |> Set.contains now.DayOfWeek) then
                $"Task '%s{task.Name}'. Today is not a working day." |> Log.warning

                if not task.Recursively then
                    do! cts.CancelAsync() |> Async.AwaitTask

            if not cts.IsCancellationRequested then
                match schedule.StopWork with
                | Some stopWork ->
                    match stopWork - now with
                    | delay when delay > TimeSpan.Zero ->
                        $"Task '%s{task.Name}'. Will be stopped at {stopWork}." |> Log.warning
                        cts.CancelAfter delay
                    | _ -> do! cts.CancelAsync() |> Async.AwaitTask
                | _ -> ()

            if not cts.IsCancellationRequested then
                match schedule.StartWork - now with
                | delay when delay > TimeSpan.Zero ->
                    $"Task '%s{task.Name}'. Will be started at {schedule.StartWork}." |> Log.warning
                    do! Async.Sleep delay
                | _ -> ()

            return cts.Token
    }
