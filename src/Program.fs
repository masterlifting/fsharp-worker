open System
open Domain.Core

[<EntryPoint>]
let main args =

    let duration =
        match args.Length with
        | 1 ->
            match args.[0] with
            | DSL.AP.IsFloat seconds -> seconds
            | _ -> (TimeSpan.FromDays 1).TotalSeconds
        | _ -> (TimeSpan.FromDays 1).TotalSeconds

    match Repository.getTasks () with
    | Error error -> $"Error: {error}" |> Log.error
    | Ok tasks ->
        let config =
            { Duration = duration
              Tasks = tasks
              Handlers = TaskStepHandlers.taskHandlers
              getTask = Repository.getTask }

        Async.RunSynchronously <| Core.startWorker config

    0
