module Core

open System
open System.Threading

open Domain.Worker
open DSL
open StepHandlers
open Logging
open System.Collections.Generic

module TaskScheduler =
    open Domain.Worker.Core

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

let doBfsSteps (steps: Core.TaskStep array) handle =
    let queue = Queue<Core.TaskStep>(steps)

    while queue.Count > 0 do
        let step = queue.Dequeue()
        handle step

        match step.Steps with
        | [] -> ()
        | _ -> step.Steps |> Seq.iter queue.Enqueue

let rec doDfsSteps (steps: Core.TaskStep list) handle =
    match steps with
    | [] -> ()
    | _ ->
        let step = steps.Head
        handle step

        match step.Steps with
        | [] -> ()
        | _ -> doDfsSteps step.Steps handle

        doDfsSteps steps.Tail handle

let private handleTaskStep taskName stepName =
    match taskName, stepName with
    | "Belgrade", CheckAvailableDates -> Belgrade.getData () |> Belgrade.processData |> Belgrade.saveData
    | "Vena", CheckAvailableDates -> Vena.getData () |> Vena.processData |> Vena.saveData
    | _ -> Error "Task was not found"

let private handleTaskSteps taskName steps (ct: CancellationToken) =

    let handle (step: Core.TaskStep) =
        if ct.IsCancellationRequested then
            ct.ThrowIfCancellationRequested()

        $"Task '{taskName}' started Step '{step.Name}'" |> Logger.logTrace

        match handleTaskStep taskName step.Name with
        | Ok _ -> $"Task '{taskName}' completed Step '{step.Name}'" |> Logger.logTrace
        | Error error -> $"Task '{taskName}' failed Step '{step.Name}'. {error}" |> Logger.logError

    doDfsSteps steps handle

let private startTask (task: Core.Task) workerCt =
    async {
        let! taskCt = TaskScheduler.getTaskExpirationToken task.Name task.Scheduler

        let rec innerLoop () =
            async {
                if not taskCt.IsCancellationRequested then

                    $"Task '{task.Name}' has been started" |> Logger.logDebug

                    do handleTaskSteps task.Name task.Steps workerCt

                    $"Task '{task.Name}' has been completed" |> Logger.logInfo

                    $"Next run of task '{task.Name}' will be in {task.Scheduler.Delay}"
                    |> Logger.logTrace

                    do! Async.Sleep task.Scheduler.Delay

                    do! innerLoop ()
                else
                    $"Task '{task.Name}' has been stopped" |> Logger.logWarning
            }

        return! innerLoop ()
    }

let startWorker (args: string[]) =
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

        match Configuration.getSection<Settings.Section> "Worker" with
        | Some settings ->
            settings
            |> Core.convertToTasks
            |> Seq.map (fun task -> startTask task cts.Token)
            |> Async.Parallel
            |> Async.RunSynchronously
            |> ignore
        | None -> failwith "Worker settings was not found"
    with
    | :? OperationCanceledException -> $"The worker has been cancelled" |> Logger.logWarning
    | ex -> ex.Message |> Logger.logError

    0
