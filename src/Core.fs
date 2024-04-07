module Core

open System
open System.Threading
open Domain.Core

let private getExpirationToken taskName scheduler =
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

let rec private handleSteps
    taskName
    (steps: TaskStep list)
    (stepHandlers: TaskStepHandler list)
    (ct: CancellationToken)
    =
    async {
        if ct.IsCancellationRequested then
            ct.ThrowIfCancellationRequested()

        match steps, stepHandlers with
        | [], _ -> ()
        | step :: stepsTail, [] ->

            $"Task '%s{taskName}'. Step '%s{step.Name}'. Handler was not found" |> Log.error

            return! handleSteps taskName step.Steps [] ct
            return! handleSteps taskName stepsTail [] ct
        | step :: stepsTail, stepHandler :: stepHandlerTail ->

            if stepHandler.Name <> step.Name then
                $"Task '%s{taskName}'. Step '%s{step.Name}'. Handler '%s{stepHandler.Name}' does not match"
                |> Log.error
            else
                $"Task '%s{taskName}'. Step '%s{step.Name}'. Started" |> Log.info

                match! stepHandler.Handle() with
                | Error error -> $"Task '%s{taskName}'. Step '%s{step.Name}'. Failed. %s{error}" |> Log.error
                | Ok msg -> $"Task '%s{taskName}'. Step '%s{step.Name}'. Completed. %s{msg}" |> Log.debug

                return! handleSteps taskName step.Steps stepHandler.Steps ct
                return! handleSteps taskName stepsTail stepHandlerTail ct
    }

let private startTask taskName taskHandlers workerCt =
    match taskHandlers |> Seq.tryFind (fun x -> x.Name = taskName) with
    | None -> async { $"Task '%s{taskName}'. Failed. Handler was not found" |> Log.error }
    | Some taskHandler ->
        let rec innerLoop () =
            async {
                match! Repository.getTask taskName with
                | Error error -> $"Task '%s{taskName}'. Failed. %s{error}" |> Log.error
                | Ok task ->
                    let! taskCt = getExpirationToken taskName task.Scheduler

                    match taskCt.IsCancellationRequested with
                    | true -> $"Task '%s{taskName}'. Stopped" |> Log.warning
                    | false ->

                        $"Task '{taskName}'. Started" |> Log.info
                        do! handleSteps taskName task.Steps taskHandler.Steps workerCt
                        $"Task '%s{taskName}'. Completed" |> Log.debug

                        $"Task '%s{taskName}'. Next run will be in {task.Scheduler.Delay}" |> Log.trace

                        do! Async.Sleep task.Scheduler.Delay
                        do! innerLoop ()
            }

        innerLoop ()

let startWorker duration handlers =
    try
        $"The worker will be running for {duration} seconds" |> Log.warning
        use cts = new CancellationTokenSource(TimeSpan.FromSeconds duration)

        match Repository.getConfiguredTaskNames () with
        | Ok taskNames ->
            taskNames
            |> Seq.map (fun taskName -> startTask taskName handlers cts.Token)
            |> Async.Parallel
            |> Async.RunSynchronously
            |> ignore
        | Error error -> failwith error
    with
    | :? OperationCanceledException -> $"The worker has been cancelled" |> Log.warning
    | ex -> ex.Message |> Log.error
