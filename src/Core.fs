module Core

module Task =
    open System
    open Infrastructure.Logging
    open Domain.Settings
    open Domain.Worker
    open Helpers
    open System.Threading
    open StepHandlers
    open Domain.Persistence

    let handle task step =
        match task, step with
        | "Belgrade", Step CheckAvailableDates -> Belgrade.getData >> Belgrade.processData >> Belgrade.saveData |> Ok
        | "Vena", Step CheckAvailableDates -> Vena.getData >> Vena.processData >> Vena.saveData |> Ok
        | _ -> Error "Task was not found"

    let private handleStep step task (ct: CancellationToken) =
        if ct.IsCancellationRequested then
            ct.ThrowIfCancellationRequested()

        $"Task '{task}' started step '{step}'" |> Logger.logTrace

        async {
            match handle step task with
            | Ok _ -> $"Task '{task}' completed step '{step}'" |> Logger.logTrace
            | Error error -> $"Task '{task}' failed step '{step}' with error: {error}" |> Logger.logError

            $"Task '{task}' completed step '{step}'" |> Logger.logTrace
        }

    let private handleSteps steps task ct =
        let handle step = handleStep step task ct
        steps |> Seq.map handle |> Async.Sequential |> Async.Ignore

    let private getDelay schedule =
        match schedule.Delay with
        | IsTimeSpan value -> value
        | _ -> TimeSpan.Zero

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
            let delay = getDelay task.Settings.Schedule

            let! taskToken = getExpirationToken task.Name task.Settings.Schedule delay

            let steps = task.Settings.Steps.Split ',' |> Seq.map Step

            let rec handleTask () =
                async {
                    if not taskToken.IsCancellationRequested then

                        $"Task '{task.Name}' has been started" |> Logger.logDebug
                        do! handleSteps steps task.Name workerToken
                        $"Task '{task.Name}' has been completed" |> Logger.logInfo

                        $"Next run of task '{task.Name}' will be in {delay}" |> Logger.logTrace
                        do! Async.Sleep delay

                        do! handleTask ()
                    else
                        $"Task '{task.Name}' has been stopped" |> Logger.logWarning
                }

            return! handleTask ()
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
                | IsInt seconds -> float seconds
                | _ -> (TimeSpan.FromDays 1).TotalSeconds
            | _ -> (TimeSpan.FromDays 1).TotalSeconds

        try
            $"The worker will be running for {duration} seconds" |> Logger.logWarning
            use cts = new CancellationTokenSource(TimeSpan.FromSeconds duration)
            start cts.Token |> Async.RunSynchronously
        with
        | :? OperationCanceledException -> $"The worker has been cancelled" |> Logger.logWarning
        | ex -> ex.Message |> Logger.logError

        0
