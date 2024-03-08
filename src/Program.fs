open System
open System.Threading
open Core
open Infrastructure.Logging
open Helpers

[<EntryPoint>]
let main args =

    let duration =
        match args.Length with
        | 1 ->
            match args.[0] with
            | IsInt seconds -> seconds
            | _ -> int (TimeSpan.FromDays 1).TotalSeconds
        | _ -> int (TimeSpan.FromDays 1).TotalSeconds

    try
        $"The worker will be running for {duration} seconds." |> Logger.logWarning
        use cts = new CancellationTokenSource(TimeSpan.FromSeconds duration)

        match startWorker cts.Token with
        | Error message -> failwith message
        | _ -> ()
    with
    | :? OperationCanceledException -> $"The worker's time was expired after {duration} seconds." |> Logger.logWarning
    | ex -> ex.Message |> Logger.logError

    0
