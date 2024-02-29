open System
open System.Threading
open Core
open Infrastructure
open Helpers.Parsers

let getDurationSec (args: string[]) =
    let defaultSeconds = int (TimeSpan.FromDays 1).TotalSeconds

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
    let di = configureWorker ()
    let logger = di.getLogger ()

    try
        $"The worker will be running for {duration} seconds." |> logger.logInfo
        let cts = new CancellationTokenSource(TimeSpan.FromSeconds duration)
        startWorker di cts.Token |> Async.RunSynchronously
    with
    | :? OperationCanceledException -> $"The worker's time was expired after {duration} seconds." |> logger.logWarning
    | ex -> ex.Message |> logger.logError

    0
