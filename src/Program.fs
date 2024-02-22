open System
open System.Threading
open Microsoft.Extensions.Configuration

let getConfig() =
    let builder: IConfigurationBuilder = new ConfigurationBuilder()
    builder.AddJsonFile("appsettings.json", optional = false, reloadOnChange = true)
        |> fun c -> c.Build()
        |> fun c -> c.GetSection("Tasks23").Value

let runTask config =
    printfn $"Task started with config: {config}"

let startWoker (cToken: CancellationToken) =
    async {
        let period = TimeSpan.FromSeconds(5.)
        let timer = new PeriodicTimer(period)
        while true do
            let!_ = timer.WaitForNextTickAsync(cToken).AsTask() |> Async.AwaitTask
            getConfig |> runTask
    }

[<EntryPoint>]
let main _ =
    let cts = new CancellationTokenSource()
    
    cts.Token
        |> startWoker
        |> Async.RunSynchronously

    0