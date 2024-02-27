module Core

open System
open System.Threading
open Microsoft.Extensions.Configuration
open Domain.Settings
open Infrastructure

let runTask (taskName: string) (task: WorkerTask) (cToken: CancellationToken) =
    let timer = new PeriodicTimer(TimeSpan.Parse(task.Schedule.WorkTime))
    
    printfn $"Starting task {taskName} with work time {task.Schedule.WorkTime}"
    
    let rec work () =
        async {
            do! timer.WaitForNextTickAsync(cToken).AsTask() |> Async.AwaitTask |> Async.Ignore
            printfn $"Running task {taskName}"
            return! work()
        }

    work()

let startWorker (config: IConfigurationRoot) (cToken: CancellationToken) =
    let settings = getConfigSection<WorkerSettings> config "Worker"
    
    settings.Tasks
        |> Seq.map (fun x -> runTask x.Key x.Value cToken)
        |> Async.Parallel
        |> Async.Ignore
