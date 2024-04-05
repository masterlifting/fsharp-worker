module Core

open System
open System.Threading
open Domain.Core

let private getExpirationToken taskName scheduler =
    async {
        let now = DateTime.UtcNow.AddHours(scheduler.TimeShift |> float)
        let cts = new CancellationTokenSource()

        if not scheduler.IsEnabled then
            $"Task '{taskName}' is disabled" |> Log.warning
            do! cts.CancelAsync() |> Async.AwaitTask

        if not cts.IsCancellationRequested then
            match scheduler.StopWork with
            | Some stopWork ->
                match stopWork - now with
                | delay when delay > TimeSpan.Zero ->
                    $"Task '{taskName}' will be stopped at {stopWork}" |> Log.warning
                    cts.CancelAfter delay
                | _ -> do! cts.CancelAsync() |> Async.AwaitTask
            | _ -> ()

        if not cts.IsCancellationRequested then
            match scheduler.StartWork - now with
            | delay when delay > TimeSpan.Zero ->
                $"Task '{taskName}' will start at {scheduler.StartWork}" |> Log.warning
                do! Async.Sleep delay
            | _ -> ()

            if scheduler.IsOnce then
                $"Task '{taskName}' will be run once" |> Log.warning
                cts.CancelAfter(scheduler.Delay.Subtract(TimeSpan.FromSeconds 1.0))

        return cts.Token
    }

let private handleSteps pScope taskName steps stepHandlers (ct: CancellationToken) =

    let taskSteps = Repository.getTaskSteps pScope 5

    let handleStep (step: TaskStep) (stepHandler: TaskStepHandler) =
        async {
            if ct.IsCancellationRequested then
                ct.ThrowIfCancellationRequested()

            if stepHandler.Name <> step.Name then
                $"Task '{taskName}'. Step '{step.Name}'. Handler '{stepHandler.Name}' does not match"
                |> Log.error
            else
                $"Task '{taskName}'. Step '{step.Name}'. Started" |> Log.info

                let! handledResult = stepHandler.Handle()

                let result =
                    match handledResult with
                    | Ok msg ->
                        $"Task '{taskName}'. Step '{step.Name}'. Completed" |> Log.debug
                        {| Status = Completed; Message = msg |}
                    | Error error ->
                        $"Task '{taskName}'. Step '{step.Name}'. Failed. {error}" |> Log.error
                        {| Status = Failed; Message = error |}

                let state =
                    { Id = step.Name
                      Status = result.Status
                      Attempts = 1
                      Message = result.Message
                      UpdatedAt = DateTime.UtcNow }

                match! Repository.saveTaskStep pScope state with
                | Error error -> $"Task '{taskName}'. Step '{step.Name}'. Failed. {error}" |> Log.error
                | Ok _ -> ()
        }

    let rec innerLoop (steps: TaskStep list) (stepHandlers: TaskStepHandler list) =
        async {
            match steps, stepHandlers with
            | [], _ -> ()
            | step :: stepsTail, [] ->
                $"Task '{taskName}'. Step '{step.Name}'. Handler was not found" |> Log.error
                return! innerLoop step.Steps []
                return! innerLoop stepsTail []
            | step :: stepsTail, stepHandler :: stepHandlerTail ->
                do! handleStep step stepHandler
                return! innerLoop step.Steps stepHandler.Steps
                return! innerLoop stepsTail stepHandlerTail
        }

    innerLoop steps stepHandlers

let private startTask taskName taskHandlers workerCt =
    match taskHandlers |> Seq.tryFind (fun x -> x.Name = taskName) with
    | None -> async { $"Task '{taskName}'. Failed. Handler was not found" |> Log.error }
    | Some taskHandler ->
        let rec innerLoop () =
            async {
                match! Repository.getTask taskName with
                | Error error -> $"Task '{taskName}'. Failed. {error}" |> Log.error
                | Ok task ->
                    let! taskCt = getExpirationToken taskName task.Scheduler

                    match taskCt.IsCancellationRequested with
                    | true -> $"Task '{taskName}'. Stopped" |> Log.warning
                    | false ->

                        let persistenceType =
                            Persistence.Type.FileStorage $"{Environment.CurrentDirectory}/tasks/{taskName}.json"

                        let persistenceScopeResult = Persistence.Scope.create persistenceType

                        match persistenceScopeResult with
                        | Error error -> $"Task '{taskName}'. Failed. {error}" |> Log.error
                        | Ok persistenceScope ->

                            $"Task '{taskName}'. Started" |> Log.info
                            do! handleSteps persistenceScope taskName task.Steps taskHandler.Steps workerCt
                            $"Task '{taskName}'. Completed" |> Log.debug

                            Persistence.Scope.remove persistenceScope

                        $"Task '{taskName}'. Next run will be in {task.Scheduler.Delay}" |> Log.trace

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
