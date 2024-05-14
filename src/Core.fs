module Worker.Core

open System
open Domain
open Domain.Core
open Infrastructure.Domain
open Worker
open Infrastructure
open Infrastructure.Logging

let private merge tasks handlers =
    
    let rec innerLoop (name: string option) (tasks: Core.Task list) (handlers: Core.TaskHandler list) =
        tasks
        |> List.map (fun task ->
            let nodeName = name |> DSL.Tree.buildNodeName <| task.Name

            match handlers |> List.tryFind (fun handler -> handler.Name = task.Name) with
            | None -> Error $"Handler %s{nodeName} was not found."
            | Some handler ->

                match innerLoop (Some nodeName) task.Steps handler.Steps with
                | Error error -> Error error
                | Ok steps ->
                    
                    
                    
                    Ok
                        { new Domain.ITreeHandler with
                            member _.Name = task.Name
                            member _.IsParallel = task.IsParallel
                            member _.Handle = handler.Handle 
                            member _.Nodes = steps })
        |> DSL.Seq.resultOrError

    innerLoop None tasks handlers

let rec private runTask getSchedule =
    fun name (task: Domain.ITreeHandler) ->
        async {
            match! getSchedule name with
            | Error error -> $"Task '%s{name}'. Failed: %s{error}" |> Log.error
            | Ok schedule ->
                let! ct = Scheduler.getExpirationToken name schedule

                match ct.IsCancellationRequested with
                | true -> $"Task '%s{name}'. Stopped." |> Log.warning
                | false ->
                    
                    $"Task '%s{name}'. Started." |> Log.info

                    match task.Handle with
                    | None -> $"Task '%s{name}'. Handler was not set." |> Log.trace
                    | Some handle ->
                        match! handle() with
                        | Error error -> $"Task '%s{name}'. Failed: %s{error}" |> Log.error
                        | Ok msg -> $"Task '%s{name}'. Successful. %s{msg}" |> Log.info

                    $"Task '%s{name}'. Completed." |> Log.info

                    match schedule with
                    | None -> ()
                    | Some schedule ->

                        $"Task '%s{name}'. Next run will be in {schedule.Delay}." |> Log.trace

                        do! Async.Sleep schedule.Delay
                        do! runTask getSchedule name task
        }

let start configure =
    async {
        Log.info "Worker started."
        match! configure() with
        | Error error -> error |> Log.error
        | Ok config ->
            match config.Tasks |> merge <| config.Handlers with
            | Error error -> error |> Log.error
            | Ok tasks ->
                let handleTask = runTask config.getSchedule
                match! DSL.Tree.doParallelOrSequential None tasks handleTask |> Async.Catch with
                | Choice1Of2 _ -> $"All tasks completed successfully." |> Log.info
                | Choice2Of2 ex ->
                    match ex with
                    | :? OperationCanceledException -> $"Worker was stopped." |> Log.warning
                    | _ -> $"Worker failed: %s{ex.Message}" |> Log.error
    }