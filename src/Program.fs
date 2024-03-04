open System
open System.Threading
open Core
open Infrastructure.Logging
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

    try
        $"The worker will be running for {duration} seconds." |> Logger.logWarning
        use cts = new CancellationTokenSource(TimeSpan.FromSeconds duration)
        Async.RunSynchronously <| startWorker cts.Token
    with
    | :? OperationCanceledException -> $"The worker's time was expired after {duration} seconds." |> Logger.logWarning
    | ex -> ex.Message |> Logger.logError

    0
