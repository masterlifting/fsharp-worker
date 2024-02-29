module Core

open System
open System.Threading
open Infrastructure
open Domain.Settings
open Domain
open Domain.Infrastructure

let runTask di task ct =
    let logger = di.getLogger ()

    let timer = new PeriodicTimer(TimeSpan.Parse task.Settings.Schedule.WorkTime)

    $"Starting task {task.Name} with work time {task.Settings.Schedule.WorkTime}"
    |> logger.logWarning

    let dbContext = di.getDbContext ()

    let rec work () =
        async {
            do! timer.WaitForNextTickAsync(ct).AsTask() |> Async.AwaitTask |> Async.Ignore
            $"Running task {task.Name}" |> logger.logInfo
            return! work ()
        }

    work ()

let startWorker di ct =
    let config = di.getConfig ()
    let settings = getConfigSection<WorkerSettings> config "Worker"

    settings.Tasks
    |> Seq.map (fun x -> runTask di { Name = x.Key; Settings = x.Value } ct)
    |> Async.Parallel
    |> Async.Ignore
