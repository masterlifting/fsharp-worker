module Core

open System
open System.Threading
open DSL
open Logging
open System.Collections.Generic

open Domain.Settings

module TaskScheduler =
    open Domain.Core

    let getTaskExpirationToken taskName scheduler =
        async {
            let now = DateTime.UtcNow.AddHours(scheduler.TimeShift |> float)
            let cts = new CancellationTokenSource()

            if not scheduler.IsEnabled then
                $"Task '{taskName}' is disabled" |> Logger.logWarning
                do! cts.CancelAsync() |> Async.AwaitTask

            if not cts.IsCancellationRequested then
                match scheduler.StopWork with
                | Some stopWork ->
                    match stopWork - now with
                    | delay when delay > TimeSpan.Zero ->
                        $"Task '{taskName}' will be stopped at {stopWork}" |> Logger.logWarning
                        cts.CancelAfter delay
                    | _ -> do! cts.CancelAsync() |> Async.AwaitTask
                | _ -> ()

            if not cts.IsCancellationRequested then
                match scheduler.StartWork - now with
                | delay when delay > TimeSpan.Zero ->
                    $"Task '{taskName}' will start at {scheduler.StartWork}" |> Logger.logWarning
                    do! Async.Sleep delay
                | _ -> ()

                if scheduler.IsOnce then
                    $"Task '{taskName}' will be run once" |> Logger.logWarning
                    cts.CancelAfter(scheduler.Delay.Subtract(TimeSpan.FromSeconds 1.0))

            return cts.Token
        }

module TaskHandler =
    open Domain.Core

    let inline private geTasktName (TaskName name) = name
    let inline private getStepName (StepName name) = name

    let handleStepsBfs (steps: TaskStep list) handleStep =
        async {
            let queue = Queue<TaskStep>(steps)

            while queue.Count > 0 do
                let step = queue.Dequeue()

                do! handleStep step

                match step.Steps with
                | [] -> ()
                | _ -> step.Steps |> Seq.iter queue.Enqueue
        }

    let rec handleStepsDfs (taskName: string) (steps: TaskStep list) (stepHandlers: TaskStepHandler list) handleStep =
        async {
            match steps with
            | [] -> ()
            | step :: stepsTail ->

                match stepHandlers with
                | [] ->
                    $"Step handler of Task '{taskName}' for Step '{step.Name}' was not found"
                    |> Logger.logError
                | stepHandler :: stepHandlerTail ->
                    do! handleStep step stepHandler
                    return! handleStepsDfs taskName step.Steps stepHandler.Steps handleStep
                    return! handleStepsDfs taskName stepsTail stepHandlerTail handleStep
        }

    let private handleTaskSteps (taskName: TaskName) steps stepHandlers (ct: CancellationToken) =
        
        let taskNameStr = taskName |> fun (TaskName name) -> name

        let handleStep (step: TaskStep) (stepHandler: TaskStepHandler) =
            async {
                if ct.IsCancellationRequested then
                    ct.ThrowIfCancellationRequested()

                if stepHandler.Name <> step.Name then
                    $"Step handler '{stepHandler.Name}' of Task '{taskName}' does not match Setting Step '{step.Name}'"
                    |> Logger.logError
                else
                    $"Task '{taskNameStr}' started Step '{step.Name}'" |> Logger.logTrace

                    match! stepHandler.Handle taskName step.Name with
                    | Ok msg -> $"Task '{taskName}' completed Step '{step.Name}'. {msg}" |> Logger.logTrace
                    | Error error -> $"Task '{taskName}' failed Step '{step.Name}'. {error}" |> Logger.logError
            }

        handleStepsDfs taskNameStr steps stepHandlers handleStep

    let internal startTask (task: Task) (taskHandlers: TaskHandler list) workerCt =
        async {
            let taskName = task.getName task.Name

            match taskHandlers |> Seq.tryFind (fun x -> x.Name = task.Name) with
            | None -> $"Handler of Task '{taskName}' was not found" |> Logger.logError
            | Some taskHandler ->
                let! taskCt = TaskScheduler.getTaskExpirationToken task.Name task.Scheduler

                let rec innerLoop () =
                    async {
                        match taskCt.IsCancellationRequested with
                        | true -> $"Task '{taskName}' has been stopped" |> Logger.logWarning
                        | false ->
                            $"Task '{taskName}' has been started" |> Logger.logDebug
                            do! handleTaskSteps task.Name task.Steps taskHandler.Steps workerCt
                            $"Task '{taskName}' has been completed" |> Logger.logInfo
                            $"Next run of task '{taskName}' will be in {task.Scheduler.Delay}"
                            |> Logger.logTrace

                            do! Async.Sleep task.Scheduler.Delay
                            do! innerLoop ()
                    }

                return! innerLoop ()
        }

let startWorker (args: string array) handlers =
    let duration =
        match args.Length with
        | 1 ->
            match args.[0] with
            | IsInt seconds -> float seconds
            | _ -> (TimeSpan.FromDays 1).TotalSeconds
        | _ -> (TimeSpan.FromDays 1).TotalSeconds

    try
        $"The worker will be running for {duration} seconds" |> Logger.logWarning
        use cts = new CancellationTokenSource(TimeSpan.FromSeconds duration)

        match Configuration.getSection<Domain.Settings.Section> "Worker" with
        | Some settings ->
            settings.Tasks
            |> Seq.map (fun taskSettings -> Domain.Core.toTask taskSettings.Key taskSettings.Value)
            |> Seq.map (fun task -> TaskHandler.startTask task handlers cts.Token)
            |> Async.Parallel
            |> Async.RunSynchronously
            |> ignore
        | None -> failwith "Worker settings was not found"
    with
    | :? OperationCanceledException -> $"The worker has been cancelled" |> Logger.logWarning
    | ex -> ex.Message |> Logger.logError

    0
