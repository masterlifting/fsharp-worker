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
    open StepHandlers

    type TaskStepHandler =
        { Name: string
          Handler: unit -> Result<string, string>
          Steps: TaskStepHandler list }

    let private taskHandlers =
        [ { Name = "Task_1"
            Handler = fun () -> Task1.getData () |> Task1.processData |> Task1.saveData
            Steps =
              [ { Name = "Step_1"
                  Handler = fun () -> Task1.getData () |> Task1.processData |> Task1.saveData
                  Steps =
                    [ { Name = "Step_1.1"
                        Handler = fun () -> Task1.getData () |> Task1.processData |> Task1.saveData
                        Steps = [] }
                      { Name = "Step_1.2"
                        Handler = fun () -> Task1.getData () |> Task1.processData |> Task1.saveData
                        Steps = [] } ] }
                { Name = "Step_1"
                  Handler = fun () -> Task1.getData () |> Task1.processData |> Task1.saveData
                  Steps = [] } ] }
          { Name = "Task_2"
            Handler = fun () -> Task2.getData () |> Task2.processData |> Task2.saveData
            Steps =
              [ { Name = "Step_1"
                  Handler = fun () -> Task2.getData () |> Task2.processData |> Task2.saveData
                  Steps = [] } ] } ]

    open Domain.Core

    open StepHandlers

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

    let rec handleStepsDfs (steps: TaskStep list) handleStep =
        async {
            match steps with
            | [] -> ()
            | step :: tail ->

                do! handleStep step

                return! handleStepsDfs step.Steps handleStep
                return! handleStepsDfs tail handleStep
        }

    let private handleTaskStep taskName stepName =
        let result =
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

        async { return result }

    let private handleTaskSteps taskName steps (ct: CancellationToken) =

        let handleStep (step: TaskStep) =
            async {
                if ct.IsCancellationRequested then
                    ct.ThrowIfCancellationRequested()

                $"Task '{taskName}' started Step '{step.Name}'" |> Logger.logTrace

                match! handleTaskStep taskName step.Name with
                | Ok _ -> $"Task '{taskName}' completed Step '{step.Name}'" |> Logger.logTrace
                | Error error -> $"Task '{taskName}' failed Step '{step.Name}'. {error}" |> Logger.logError
            }

        handleStepsDfs steps handleStep

    let internal startTask task workerCt =
        async {
            let! taskCt = TaskScheduler.getTaskExpirationToken task.Name task.Scheduler

            let rec innerLoop () =
                async {
                    if not taskCt.IsCancellationRequested then

                        $"Task '{task.Name}' has been started" |> Logger.logDebug

                        do! handleTaskSteps task.Name task.Steps workerCt

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

let startWorker (args: string array) =
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
            |> Seq.map (fun task -> TaskHandler.startTask task cts.Token)
            |> Async.Parallel
            |> Async.RunSynchronously
            |> ignore
        | None -> failwith "Worker settings was not found"
    with
    | :? OperationCanceledException -> $"The worker has been cancelled" |> Logger.logWarning
    | ex -> ex.Message |> Logger.logError

    0
