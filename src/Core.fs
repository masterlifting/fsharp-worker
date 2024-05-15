module Worker.Core

open System
open Domain
open Domain.Core
open Infrastructure
open Infrastructure.Logging
open Infrastructure.Domain.Graph
open Worker

let private merge tasks handlers =
    
    let rec innerLoop nodeName (tasks: Node<Task> list) (handlers: Node<TaskHandler> list) =
        tasks
        |> List.map (fun task ->
            let nodeName = nodeName |> DSL.Graph.buildNodeName <| task.Value.Name

            match handlers |> List.tryFind (fun handler -> handler.Value.Name = task.Value.Name) with
            | None -> Error $"Handler %s{nodeName} was not found."
            | Some handler ->

                match innerLoop (Some nodeName) task.Children handler.Children with
                | Error error -> Error error
                | Ok steps ->
                    Ok <| Node( { new INodeHandle with
                                member _.Name = task.Value.Name
                                member _.IsParallel = task.Value.IsParallel
                                member _.Handle = handler.Value.Handle }, steps ))  
        |> DSL.Seq.resultOrError

    innerLoop None tasks handlers

let rec private runTask getSchedule =
    fun (task: INodeHandle) ->
        async {
            let name = task.Name
            
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
                        do! runTask getSchedule task
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
                match! DSL.Graph.handle' None tasks handleTask |> Async.Catch with
                | Choice1Of2 _ -> $"All tasks completed successfully." |> Log.info
                | Choice2Of2 ex ->
                    match ex with
                    | :? OperationCanceledException -> $"Worker was stopped." |> Log.warning
                    | _ -> $"Worker failed: %s{ex.Message}" |> Log.error
    }