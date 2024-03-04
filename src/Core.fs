module Core

open System
open Infrastructure
open Infrastructure.Logging
open Domain.Settings
open Domain.Worker

let stepHandler: WorkerTaskStepHandler =
    Map["Belgrade", Map["CheckAvailableDates", (fun () -> async { return Ok "Belgrade - CheckAvailableDates Info" })]]

let runTask task ct =

    let hendleStep step =
        async {

            $"Task {task.Name} started the step {step}" |> Logger.logTrace

            let! result =
                match stepHandler.TryFind task.Name with
                | Some t ->
                    match t.TryFind step with
                    | Some handle -> handle ()
                    | None -> async { return Error("Step handler was not found") }
                | None -> async { return Error("Task handler was not found") }

            match result with
            | Ok i -> $"Task {task.Name} completed step {step}. Info: {i}" |> Logger.logDebug
            | Error e -> $"Task {task.Name} failed step {step}. Reason: {e}" |> Logger.logError
        }

    let steps = task.Settings.Steps.Split ','
    let period = TimeSpan.Parse task.Settings.Schedule.WorkTime

    let rec innerLoop () =
        async {
            do! Async.Sleep period

            $"Task {task.Name} has been started" |> Logger.logDebug

            for step in steps do
                do! hendleStep step

            $"Task {task.Name} has been completed" |> Logger.logInfo

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
