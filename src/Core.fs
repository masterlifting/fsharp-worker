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

    let private handleStep taskName step (ct: CancellationToken) =
        if ct.IsCancellationRequested then
            ct.ThrowIfCancellationRequested()

        $"Task '{taskName}' started step '{step}'" |> Logger.logTrace

        async {
            match taskStepHandlers.TryFind taskName with
            | Some stepHandlers ->
                match stepHandlers.TryFind step with
                | Some handle ->
                    match! handle () with
                    | Ok _ -> $"Task '{taskName}' completed step '{step}'" |> Logger.logTrace
                    | Error error -> $"Task '{taskName}' failed step '{step}' with error: {error}" |> Logger.logError
                | None -> "Step handler was not found" |> Logger.logError
            | None -> "Task handler was not found" |> Logger.logError
        }

    let private handleSteps taskName (steps: string) ct =
        steps.Split ','
        |> Seq.map (fun step -> handleStep taskName step ct)
        |> Async.Sequential
        |> Async.Ignore

    let private getExpirationToken settings =
        async {
            let now = DateTime.UtcNow.AddHours(float settings.Schedule.TimeShift)
            let cts = new CancellationTokenSource()

            match settings.Schedule.StopWork with
            | HasValue stopWork ->
                match stopWork - now with
                | ts when ts > TimeSpan.Zero -> cts.CancelAfter ts
                | _ -> cts.Cancel()
            | _ -> ()

            if not cts.IsCancellationRequested then
                match settings.Schedule.StartWork with
                | HasValue startWork ->
                    match startWork - now with
                    | ts when ts > TimeSpan.Zero -> do! Async.Sleep ts
                    | _ -> ()
                | _ -> ()

            return cts.Token
        }

    let start task ct =
        async {
            let delay =
                match task.Settings.Schedule.WorkTime with
                | IsTimeSpan result -> result
                | _ -> TimeSpan.Zero

            let! expToken = getExpirationToken task.Settings

            let rec innerLoop () =
                async {
                    if not expToken.IsCancellationRequested then
                        $"Task '{task.Name}' has been started" |> Logger.logDebug
                        do! handleSteps task.Name task.Settings.Steps ct
                        $"Task '{task.Name}' has been completed" |> Logger.logInfo
                        do! Async.Sleep delay
                        do! innerLoop ()
                    else
                        $"Task '{task.Name}' has been stopped" |> Logger.logWarning
                }

            return! innerLoop ()
        }

open Infrastructure
open Domain.Settings

let startWorker ct =
    match Configuration.getSection<WorkerSettings> "Worker" with
    | Some settings ->
        settings.Tasks
        |> Seq.map (fun x -> Task.start { Name = x.Key; Settings = x.Value } ct)
        |> Async.Parallel
        |> Async.Ignore
        |> Ok
    | None -> Error "Worker settings was not found"
