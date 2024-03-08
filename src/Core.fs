module Core

module Task =
    open System
    open Infrastructure.Logging
    open Domain.Settings
    open Domain.Worker
    open Helpers
    open System.Threading

    let private taskStepHandlers =
        Map["Belgrade", StepHandlers.Belgrade.Handler
            "Vena", StepHandlers.Vena.Handler]

    let private handleStep step task (ct: CancellationToken) =
        if ct.IsCancellationRequested then
            ct.ThrowIfCancellationRequested()

        $"Task '{task}' started step '{step}'" |> Logger.logTrace

        async {
            match taskStepHandlers.TryFind task with
            | Some stepHandlers ->
                match stepHandlers.TryFind step with
                | Some handle ->
                    match! handle () with
                    | Ok _ -> $"Task '{task}' completed step '{step}'" |> Logger.logTrace
                    | Error error -> $"Task '{task}' failed step '{step}' with error: {error}" |> Logger.logError
                | None -> "Step handler was not found" |> Logger.logError
            | None -> "Task handler was not found" |> Logger.logError
        }

    let private getExpirationToken task schedule (delay: TimeSpan) =
        async {
            let now = DateTime.UtcNow.AddHours(float schedule.TimeShift)
            let cts = new CancellationTokenSource()

            if not schedule.IsEnabled then
                $"Task '{task}' is disabled" |> Logger.logWarning
                do! cts.CancelAsync() |> Async.AwaitTask

            if not cts.IsCancellationRequested then
                match schedule.StopWork with
                | HasValue stopWork ->
                    match stopWork - now with
                    | ts when ts > TimeSpan.Zero ->
                        $"Task '{task}' will be stopped at {stopWork}" |> Logger.logWarning
                        cts.CancelAfter ts
                    | _ -> do! cts.CancelAsync() |> Async.AwaitTask
                | _ -> ()

            if not cts.IsCancellationRequested then
                match schedule.StartWork with
                | HasValue startWork ->
                    match startWork - now with
                    | ts when ts > TimeSpan.Zero ->
                        $"Task '{task}' will start at {startWork}" |> Logger.logWarning
                        do! Async.Sleep ts
                    | _ -> ()
                | _ -> ()

                if schedule.IsOnce then
                    $"Task '{task}' will be run once" |> Logger.logWarning
                    cts.CancelAfter(delay.Subtract(TimeSpan.FromSeconds 1.0))

            return cts.Token
        }

    let start task workerToken =
        async {
            let delay =
                match task.Settings.Schedule.Delay with
                | IsTimeSpan value' -> value'
                | _ -> TimeSpan.Zero

            let! taskToken = getExpirationToken task.Name task.Settings.Schedule delay

            let steps = task.Settings.Steps.Split ','

            let rec innerLoop () =
                async {
                    if not taskToken.IsCancellationRequested then
                        $"Task '{task.Name}' has been started" |> Logger.logDebug

                        do!
                            steps
                            |> Seq.map (fun step -> handleStep step task.Name workerToken)
                            |> Async.Sequential
                            |> Async.Ignore

                        $"Task '{task.Name}' has been completed" |> Logger.logInfo
                        $"Next run of task '{task.Name}' will be in {delay}" |> Logger.logTrace
                        do! Async.Sleep delay
                        do! innerLoop ()
                    else
                        $"Task '{task.Name}' has been stopped" |> Logger.logWarning
                }

            return! innerLoop ()
        }

module Worker =
    open Infrastructure
    open Domain.Settings
    open System
    open System.Threading
    open Helpers
    open Infrastructure.Logging

    let private start ct =
        match Configuration.getSection<WorkerSettings> "Worker" with
        | Some settings ->
            settings.Tasks
            |> Seq.map (fun x -> Task.start { Name = x.Key; Settings = x.Value } ct)
            |> Async.Parallel
            |> Async.Ignore
        | None -> failwith "Worker settings was not found"

    let work (args: string[]) =
        let duration =
            match args.Length with
            | 1 ->
                match args.[0] with
                | IsInt seconds -> seconds
                | _ -> int (TimeSpan.FromDays 1).TotalSeconds
            | _ -> int (TimeSpan.FromDays 1).TotalSeconds

        try
            $"The worker will be running for {duration} seconds" |> Logger.logWarning
            use cts = new CancellationTokenSource(TimeSpan.FromSeconds duration)
            start cts.Token |> Async.RunSynchronously
        with
        | :? OperationCanceledException -> $"The worker has been cancelled" |> Logger.logWarning
        | ex -> ex.Message |> Logger.logError

        0
