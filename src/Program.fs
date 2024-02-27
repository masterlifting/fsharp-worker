open System
open System.Threading
open Microsoft.Extensions.Configuration
open Domain.Configurations

let getConfiguration() =
    let builder = new ConfigurationBuilder()
    builder.AddJsonFile("appsettings.json", optional = false, reloadOnChange = true).Build()

let getSettings<'T> (config: IConfigurationRoot, section: string) =
    config.GetSection(section).Get<'T>()

let runTask (name: string, task: WorkerTask, cToken: CancellationToken) = async {
    let timer = new PeriodicTimer(TimeSpan.Parse(task.Schedule.WorkTime))
    
    printfn $"Starting task {name} with work time {task.Schedule.WorkTime}"
    
    while not cToken.IsCancellationRequested do
        let! _ = timer.WaitForNextTickAsync(cToken).AsTask() |> Async.AwaitTask
        printfn $"Running task {name}"
}

let runTasks (config: IConfigurationRoot, cToken: CancellationToken) =
    getSettings<WorkerSettings>(config, "Worker").Tasks
        |> Seq.map (fun x -> runTask(x.Key, x.Value, cToken))
        |> Async.Parallel
        |> Async.Ignore

[<EntryPoint>]
let main _ =
    let cts = new CancellationTokenSource()
    let config = getConfiguration()
    
    runTasks(config, cts.Token) |> Async.RunSynchronously

    0