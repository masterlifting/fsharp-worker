open System
open System.Threading
open Microsoft.Extensions.Configuration
open Domain.Configurations
open System.Text.Json

let getSettings() =
    let builder: IConfigurationBuilder = new ConfigurationBuilder()
    builder.AddJsonFile("appsettings.json", optional = false, reloadOnChange = true)
        |> fun c -> c.Build()
        |> fun c -> c.GetSection("Worker").Value
        |> JsonSerializer.Deserialize<WorkerSettings>
        
let runTask (name: string) (config: TAskItem) =
    printfn $"Task {name} is running with config {config}"

let start (cToken: CancellationToken) =
    async {
        let period = TimeSpan.FromSeconds(5.)
        let timer = new PeriodicTimer(period)
        while not cToken.IsCancellationRequested do
            let!_ = timer.WaitForNextTickAsync(cToken).AsTask() |> Async.AwaitTask
            getSettings() |> runTask
    }

[<EntryPoint>]
let main _ =
    let cts = new CancellationTokenSource()
    
    cts.Token
        |> start
        |> Async.RunSynchronously

    0