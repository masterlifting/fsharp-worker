open System
open System.Threading
open Core
open Infrastructure

[<EntryPoint>]
let main _ =
    let cts = new CancellationTokenSource(TimeSpan.FromSeconds(10.0))
    let config = getConfig()
    
    try
        startWorker config cts.Token |> Async.RunSynchronously
    with
        | :? OperationCanceledException -> printfn "The worker's time was expired."
        | ex -> printfn $"Error: {ex.Message}."

    0