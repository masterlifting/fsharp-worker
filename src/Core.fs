module Core

module Task =
    open System
    open Infrastructure.Logging
    open Domain.Settings
    open Domain.Worker
    open Helpers
    open System.Threading
    open Domain.Persistence
    open StepHandlers

    let getData task step =
        match task, step with
        | "Belgrade", CheckAvailableDates -> [| new Kdmid(Guid.NewGuid(), 1, 1, 0, None, DateTime.Now) :> IWorkerData |]
        | "Vena", CheckAvailableDates -> [| new Kdmud(Guid.NewGuid(), 1, 1, 0, None, DateTime.Now) :> IWorkerData |]
        | _ -> [||]

    let handleData task step data =
        match task, step with
        | "Belgrade", CheckAvailableDates ->
            let data' = data |> Seq.cast<Kdmid> |> Seq.toArray
            let result = Belgrade.checkAvailableDates data'

            match result with
            | Ok result -> result |> Array.map (fun x -> x :> IWorkerData)
            | Error error -> [||]
        | "Vena", CheckAvailableDates ->
            let data' = data |> Seq.cast<Kdmud> |> Seq.toArray
            let result = Vena.checkAvailableDates data'

            match result with
            | Ok result -> result |> Array.map (fun x -> x :> IWorkerData)
            | Error error -> [||]
        | _ -> failwith "Task was not found"

    let saveData task step data =
        match task, step with
        | "Belgrade", CheckAvailableDates -> Ok
        | "Vena", CheckAvailableDates -> Ok
        | _ -> failwith "Task was not found"

    let private handleStep step task (ct: CancellationToken) =
        if ct.IsCancellationRequested then
            ct.ThrowIfCancellationRequested()

        $"Task '{task}' started step '{step}'" |> Logger.logTrace

        let proccess = getData task step |> handleData task step |> saveData task step

        async {
            match proccess () with
            | Ok _ -> $"Task '{task}' completed step '{step}'" |> Logger.logTrace
            | Error error -> $"Task '{task}' failed step '{step}' with error: {error}" |> Logger.logError

            $"Task '{task}' completed step '{step}'" |> Logger.logTrace
        }

    let handleSteps steps task ct =
        let handle step = handleStep step task ct
        steps |> Seq.map handle |> Async.Sequential |> Async.Ignore

    let getDelay schedule =
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

            let steps = task.Settings.Steps.Split ','

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
