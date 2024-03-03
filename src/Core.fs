module Core

open System
open Infrastructure
open Domain.Settings
open Domain
open Domain.Infrastructure

let runTask di task ct =
    let logger = di.getLogger ()

    let period = TimeSpan.Parse <| task.Settings.Schedule.WorkTime

    $"Task {task.Name} with period {task.Settings.Schedule.WorkTime} started"
    |> logger.logWarning

    let taskSteps = task.Settings.Steps.Split [| ',' |]

    "Task steps: " + String.Join(", ", taskSteps) |> logger.logInfo

    let handle name step di ct =
        async {
            $"Task {name} step {step} started" |> logger.logInfo
            let dbContext = di.GetDbContext()
            return! Ok()
        }


    let rec hendleStep step =
        async {

            $"Step {step} started" |> logger.logInfo

            let! result = handle task.Name step di ct

            match result with
            | Ok _ -> $"Step {step} completed successfully" |> logger.logInfo
            | Error e -> $"Step {step} failed with error: {e}" |> logger.logError
        }

    let rec work () =
        async {
            do! Async.Sleep period

            $"Task {task.Name} started" |> logger.logInfo

            for step in taskSteps do
                do! hendleStep step

            $"Task {task.Name} completed" |> logger.logInfo

            return! work ()
        }

    work ()

let startWorker di ct =
    let settings = getConfigSection<WorkerSettings> "Worker"

    settings.Tasks
    |> Seq.map (fun x -> runTask di { Name = x.Key; Settings = x.Value } ct)
    |> Async.Parallel
    |> Async.Ignore
