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
    let di = configureWorker ()

    let logger = di.getLogger ()

    try
        logger.logWarning $"The worker will be running for {duration} seconds."

        let cts = new CancellationTokenSource(TimeSpan.FromSeconds duration)
        startWorker di cts.Token |> Async.RunSynchronously
    with
    | :? OperationCanceledException -> logger.logWarning $"The worker's time was expired after {duration} seconds."
    | ex -> logger.logError ex.Message

    0
