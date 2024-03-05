module Core

open System
open Infrastructure
open Infrastructure.Logging
open Domain.Settings
open Domain.Worker
open Helpers
open System.Threading

let private taskStepHandler =
    Map["Belgrade", StepHandlers.Belgrade.Handler
        "Vena", StepHandlers.Vena.Handler]

let startWorker (ct: CancellationToken) =

    let runTaskStep task step =
        if ct.IsCancellationRequested then
            ct.ThrowIfCancellationRequested()

        match taskStepHandler.TryFind task.Name with
        | Some steps ->
            match steps.TryFind step with
            | Some handle -> handle ()
            | None -> async { return Error("Step handler was not found") }
        | None -> async { return Error("Task handler was not found") }

    let runTask task =
        async {
            let now = DateTime.UtcNow.AddHours(float task.Settings.Schedule.TimeShift)
            let cts = new CancellationTokenSource()

            let delay =
                match task.Settings.Schedule.WorkTime with
                | IsTimeSpan result -> result
                | _ -> TimeSpan.Zero

            match task.Settings.Schedule.StopWork with
            | HasValue stopWork ->
                match stopWork - now with
                | ts when ts > TimeSpan.Zero -> cts.CancelAfter ts
                | _ -> cts.Cancel()
            | _ -> ()

            if not cts.IsCancellationRequested then
                match task.Settings.Schedule.StartWork with
                | HasValue startWork ->
                    match startWork - now with
                    | ts when ts > TimeSpan.Zero -> do! Async.Sleep ts
                    | _ -> ()
                | _ -> ()

            let steps = task.Settings.Steps.Split ','

            let rec innerLoop () =
                async {
                    if cts.Token.IsCancellationRequested then
                        return! async { $"Task '{task.Name}' has been stopped" |> Logger.logWarning }
                    else
                        $"Task '{task.Name}' has been started" |> Logger.logDebug

                        for step in steps do
                            $"Task '{task.Name}' started step '{step}'" |> Logger.logTrace

                            let! stepResult = runTaskStep task step

                            match stepResult with
                            | Ok i -> $"Task '{task.Name}' completed step '{step}'. Info: {i}" |> Logger.logDebug
                            | Error e -> $"Task '{task.Name}' failed step '{step}'. Reason: {e}" |> Logger.logError

                        $"Task '{task.Name}' has been completed" |> Logger.logInfo

                        do! Async.Sequential [ Async.Sleep delay; innerLoop () ] |> Async.Ignore
                }

            return! innerLoop ()
        }

    match Configuration.getSection<WorkerSettings> "Worker" with
    | Some settings ->
        settings.Tasks
        |> Seq.map (fun x -> runTask { Name = x.Key; Settings = x.Value })
        |> Async.Parallel
        |> Async.Ignore
    | None -> failwith "Worker settings was not found"
