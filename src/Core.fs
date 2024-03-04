module Core

open System
open Infrastructure
open Infrastructure.Logging
open Domain.Settings
open Domain.Worker
open WorkerTasks
open System.Threading

let private taskStepHandler =
    Map["Belgrade", Belgrade.StepHandler
        "Vena", Vena.StepHandler]

let private hendleTaskStep task step (ct: CancellationToken) =
    async {
        return!
            match ct.IsCancellationRequested with
            | true -> async { return Error("Task was cancelled") }
            | false ->
                match taskStepHandler.TryFind task with
                | Some steps ->
                    match steps.TryFind step with
                    | Some handle -> handle ()
                    | None -> async { return Error("Step handler was not found") }
                | None -> async { return Error("Task handler was not found") }
    }

let private runTask task ct =
    let steps = task.Settings.Steps.Split ','
    let period = TimeSpan.Parse task.Settings.Schedule.WorkTime

    let rec innerLoop () =
        async {
            do! Async.Sleep period

            $"Task '{task.Name}' has been started" |> Logger.logDebug

            for step in steps do
                $"Task '{task.Name}' started step '{step}'" |> Logger.logTrace

                let! stepResult = hendleTaskStep task.Name step ct

                match stepResult with
                | Ok i -> $"Task '{task.Name}' completed step '{step}'. Info: {i}" |> Logger.logDebug
                | Error e -> $"Task '{task.Name}' failed step '{step}'. Reason: {e}" |> Logger.logError

            $"Task '{task.Name}' has been completed" |> Logger.logInfo

            return! innerLoop ()
        }

    innerLoop ()

let startWorker ct =
    let settings = Configuration.getSection<WorkerSettings> "Worker"

    match settings with
    | Some settings ->
        settings.Tasks
        |> Seq.map (fun x -> runTask { Name = x.Key; Settings = x.Value } ct)
        |> Async.Parallel
        |> Async.Ignore
    | None -> failwith "Worker settings was not found"
