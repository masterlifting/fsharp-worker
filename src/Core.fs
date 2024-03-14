module Core

open System
open Infrastructure.Logging
open Domain.Settings
open Domain.Worker
open Helpers
open System.Threading
open StepHandlers
open Infrastructure
open Scheduler

let private handleTaskStep taskName step =
    match taskName, step with
    | "Belgrade", Step CheckAvailableDates -> Belgrade.getData () |> Belgrade.processData |> Belgrade.saveData
    | "Vena", Step CheckAvailableDates -> Vena.getData () |> Vena.processData |> Vena.saveData
    | _ -> Error "Task was not found"

let private handleTaskSteps taskName steps (ct: CancellationToken) =

    steps
    |> Seq.map (fun step ->
        if ct.IsCancellationRequested then
            ct.ThrowIfCancellationRequested()

        $"Task '{taskName}' started {step}" |> Logger.logTrace

        async {
            match handleTaskStep taskName step with
            | Ok _ -> $"Task '{taskName}' completed {step}" |> Logger.logTrace
            | Error error -> $"Task '{taskName}' failed {step} with error: {error}" |> Logger.logError

            $"Task '{taskName}' completed {step}" |> Logger.logTrace
        })
    |> Async.Sequential
    |> Async.Ignore

let private startTask task workerCt =
    async {
        let delay = getTaskDelay task.Settings.Schedule

        let! taskCt = getTaskExpirationToken task.Name delay task.Settings.Schedule

        let steps = task.Settings.Steps.Split ',' |> Seq.map Step

        let rec innerLoop () =
            async {
                if not taskCt.IsCancellationRequested then

                    $"Task '{task.Name}' has been started" |> Logger.logDebug
                    do! handleTaskSteps task.Name steps workerCt
                    $"Task '{task.Name}' has been completed" |> Logger.logInfo

                    $"Next run of task '{task.Name}' will be in {delay}" |> Logger.logTrace
                    do! Async.Sleep delay

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

        match Configuration.getSection<WorkerSettings> "Worker" with
        | Some settings ->
            settings.Tasks
            |> Seq.map (fun x -> startTask { Name = x.Key; Settings = x.Value } cts.Token)
            |> Async.Parallel
            |> Async.RunSynchronously
            |> ignore
        | None -> failwith "Worker settings was not found"
    with
    | :? OperationCanceledException -> $"The worker has been cancelled" |> Logger.logWarning
    | ex -> ex.Message |> Logger.logError

    0
