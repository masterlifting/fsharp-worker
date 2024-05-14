module Worker.Core

open System
open Domain
open Domain.Core
open Infrastructure.Domain
open Worker
open Infrastructure
open Infrastructure.Logging

let private merge tasks handlers =
    
    let rec innerLoop (name: string option) (tasks: Graph<Core.Task> list) (handlers: Graph<Core.TaskHandler> list) =
        tasks
        |> List.map (fun task ->
            let nodeName = name |> DSL.Graph.buildNodeName <| task.current.Name

            match handlers |> List.tryFind (fun handler -> handler.current.Name = task.current.Name) with
            | None -> Error $"Handler %s{nodeName} was not found."
            | Some handler ->

                match innerLoop (Some nodeName) task.nodes handler.nodes with
                | Error error -> Error error
                | Ok steps ->
                    let graph =
                        Graph( { new IHandle with
                                    member _.Name = task.current.Name
                                    member _.IsParallel = task.current.IsParallel
                                    member _.Handle = handler.current.Handle }, steps )
                    Ok graph )  
        |> DSL.Seq.resultOrError

    innerLoop None tasks handlers

let rec private runTask getSchedule =
    fun name (task: IHandle) ->
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
                match! DSL.Graph.doParallelOrSequential None tasks handleTask |> Async.Catch with
                | Choice1Of2 _ -> $"All tasks completed successfully." |> Log.info
                | Choice2Of2 ex ->
                    match ex with
                    | :? OperationCanceledException -> $"Worker was stopped." |> Log.warning
                    | _ -> $"Worker failed: %s{ex.Message}" |> Log.error
    }