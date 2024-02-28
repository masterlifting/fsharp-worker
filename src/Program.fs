open System
open System.Threading
open Core
open Infrastructure
open Helpers.Parsers

let getDurationSec (args: string[]) =
    let defaultSeconds = Int32.MaxValue

    match args.Length with
    | 1 ->
        match args.[0] with
        | IntParse arg ->
            match arg with
            | Some result -> result
            | _ -> defaultSeconds
    | _ -> defaultSeconds

[<EntryPoint>]
let main args =

    let duration = getDurationSec args

    try
        let di = configureWorker ()
        let cts = new CancellationTokenSource(TimeSpan.FromSeconds duration)

        startWorker di cts.Token |> Async.RunSynchronously
    with
    | :? OperationCanceledException -> printfn $"The worker's time was expired after {duration} seconds."
    | ex -> printfn $"Error: {ex.Message}."

    0
