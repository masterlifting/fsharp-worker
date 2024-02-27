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
    let schedule = task.Schedule
    let period = TimeSpan.Parse(schedule.WorkTime)
    let timer = new PeriodicTimer(period)
    
    printfn $"Starting task {name} with work time {task.Schedule.WorkTime}"
    while not cToken.IsCancellationRequested do
        let! _ = timer.WaitForNextTickAsync(cToken).AsTask() |> Async.AwaitTask
        printfn $"Running task {name}"
}

let runTasks (config: IConfigurationRoot, cToken: CancellationToken) =
    async {
        let workerSettings = (config, "Worker") |> getSettings<WorkerSettings>
        
        workerSettings.Tasks
            |> Seq.map (fun x -> (x.Key, x.Value, cToken) |> runTask)
            |> Async.Parallel
            |> Async.RunSynchronously
            |> ignore
    }

[<EntryPoint>]
let main _ =
    let cts = new CancellationTokenSource()
    let config = getConfiguration()
    
    (config, cts.Token)
        |> runTasks
        |> Async.RunSynchronously

    0