module Worker.Core

open System
open System.Threading
open Domain
open Domain.Core
open Worker
open Infrastructure.Logging

let rec private handleSteps taskName (steps: TaskStep list) (ct: CancellationToken) =
    async {
        if ct.IsCancellationRequested then
            ct.ThrowIfCancellationRequested()

        if steps.Length > 0 then
            match steps |> List.takeWhile (fun step -> step.IsParallel) with
            | parallelSteps when parallelSteps.Length < 2 ->

                let sequentialSteps =
                    steps |> List.take 1 |> List.takeWhile (fun step -> not step.IsParallel)

                do!
                    sequentialSteps
                    |> List.map (fun step -> handleStep taskName step ct)
                    |> Async.Sequential
                    |> Async.Ignore

                do! handleSteps taskName (steps |> List.skip sequentialSteps.Length) ct

            | parallelSteps ->

                do!
                    parallelSteps
                    |> List.map (fun step -> handleStep taskName step ct)
                    |> Async.Parallel
                    |> Async.Ignore

                do! handleSteps taskName (steps |> List.skip parallelSteps.Length) ct
    }

and private handleStep taskName step ct =
    async {
        $"Task '%s{taskName}'. Step '%s{step.Name}'. Started." |> Log.info

        match! step.Handle() with
        | Error error -> $"Task '%s{taskName}'. Step '%s{step.Name}'. Failed: %s{error}" |> Log.error
        | Ok msg -> $"Task '%s{taskName}'. Step '%s{step.Name}'. Completed. %s{msg}" |> Log.debug

        do! handleSteps taskName step.Steps ct
    }

let rec private mergeStepsHandlers (steps: TaskStepSettings list) (handlers: TaskStepHandler list) =
    steps
    |> List.map (fun step ->
        match handlers |> List.tryFind (fun handler -> handler.Name = step.Name) with
        | None -> Error $"Handler %s{step.Name} was not found."
        | Some handler ->
            match mergeStepsHandlers step.Steps handler.Steps with
            | Error error -> Error error
            | Ok steps ->
                Ok
                    { Name = step.Name
                      IsParallel = step.IsParallel
                      Handle = handler.Handle
                      Steps = steps })
    |> Infrastructure.DSL.Seq.resultOrError

let rec private startTask
    taskName
    (handler: TaskHandler)
    workerCt
    (getTask: string -> Async<Result<TaskSettings, string>>)
    =
    async {
        match! getTask taskName with
        | Error error -> $"Task '%s{taskName}'. Failed: %s{error}" |> Log.error
        | Ok task ->
            let! taskCt = Scheduler.getExpirationToken taskName task.Scheduler

            match taskCt.IsCancellationRequested with
            | true -> $"Task '%s{taskName}'. Stopped." |> Log.warning
            | false ->
                match mergeStepsHandlers task.Steps handler.Steps with
                | Error error -> $"Task '%s{taskName}'. Failed: %s{error}" |> Log.error
                | Ok steps ->

                    $"Task '%s{taskName}'. Started." |> Log.info
                    do! handleSteps taskName steps workerCt
                    $"Task '%s{taskName}'. Completed." |> Log.debug

                    $"Task '%s{taskName}'. Next run will be in {task.Scheduler.Delay}." |> Log.trace

                    do! Async.Sleep task.Scheduler.Delay
                    do! startTask taskName handler workerCt getTask
    }

let start config =
    async {
        match! config with
        | Error error -> error |> Log.error
        | Ok config ->
            let! result =
                config.Tasks
                |> Seq.map (fun task ->
                    match config.Handlers |> Seq.tryFind (fun x -> x.Name = task.Name) with
                    | Some handler -> startTask task.Name handler config.CancellationToken config.getTask
                    | None -> async { return $"Task '%s{task.Name}'. Failed: Handler was not found." |> Log.error })
                |> Async.Parallel
                |> Async.Catch

            match result with
            | Choice1Of2 _ -> $"All tasks completed successfully." |> Log.info
            | Choice2Of2 ex ->
                match ex with
                | :? OperationCanceledException -> $"Worker was stopped." |> Log.warning
                | _ -> $"Worker failed: %s{ex.Message}" |> Log.error
    }
