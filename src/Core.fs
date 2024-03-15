module Core

open System
open System.Threading

open Domain.Worker
open Infrastructure.Logging
open Helpers
open StepHandlers
open Infrastructure
open System.Collections.Generic

let doBfsSteps (steps: TaskStep[]) handle =
    let queue = Queue<TaskStep>(steps)

    while queue.Count > 0 do
        let step = queue.Dequeue()
        handle step

        match step.Steps with
        | [||] -> ()
        | _ -> step.Steps |> Seq.iter queue.Enqueue

let rec doDfsSteps (steps: TaskStep[]) handle =
    match steps with
    | [||] -> ()
    | _ ->
        let step = steps.[0]
        handle step

        match step.Steps with
        | [||] -> ()
        | _ -> doDfsSteps step.Steps handle

        doDfsSteps steps.[1..] handle

let private handleTaskStep taskName stepName =
    match taskName with
    | "Task_1" ->
        match stepName with
        | "Step_1.2" -> Task1.getData () |> Task1.processData |> Task1.saveData
        | _ -> Error $"'{stepName}' was not found in '{taskName}'"
    | "Task_2" ->
        match stepName with
        | "Step_1" -> Task2.getData () |> Task2.processData |> Task2.saveData
        | _ -> Error $"'{stepName}' was not found in '{taskName}'"
    | _ -> Error $"'{taskName}' was not found"

let private handleTaskSteps taskName steps (ct: CancellationToken) =

    let handle (step: TaskStep) =
        if ct.IsCancellationRequested then
            ct.ThrowIfCancellationRequested()

        $"Task '{taskName}' started Step '{step.Name}'" |> Logger.logTrace

        match handleTaskStep taskName step.Name with
        | Ok _ -> $"Task '{taskName}' completed Step '{step.Name}'" |> Logger.logTrace
        | Error error -> $"Task '{taskName}' failed Step '{step.Name}'. {error}" |> Logger.logError

    doDfsSteps steps handle

let private startTask task workerCt =
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
            |> convertToTasks
            |> Seq.map (fun task -> startTask task cts.Token)
            |> Async.Parallel
            |> Async.RunSynchronously
            |> ignore
        | None -> failwith "Worker settings was not found"
    with
    | :? OperationCanceledException -> $"The worker has been cancelled" |> Logger.logWarning
    | ex -> ex.Message |> Logger.logError

    0
