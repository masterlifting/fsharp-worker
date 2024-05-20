module Worker.Core

open System
open Domain
open Domain.Core
open Infrastructure
open Infrastructure.Logging
open Infrastructure.Domain.Graph
open Worker
open System.Threading

let private merge tasks handlers =

    let rec innerLoop nodeName (tasks: Node<Task> list) (handlers: Node<TaskHandler> list) =
        tasks
        |> List.map (fun task ->
            let name = nodeName |> DSL.Graph.buildNodeName <| task.Value.Name

            match handlers |> List.tryFind (fun handler -> handler.Value.Name = task.Value.Name) with
            | None -> Error $"Handler %s{name} was not found."
            | Some handler ->

                match innerLoop (Some name) task.Children handler.Children with
                | Error error -> Error error
                | Ok steps ->

                    if handler.Value.Handle.IsNone then
                        $"Task '%s{name}'. Handler was not set." |> Log.warning

                    Ok
                    <| Node(
                        { new INodeHandle with
                            member _.Name = name
                            member _.Parallel = task.Value.Parallel
                            member _.Recurcive = task.Value.Recurcive
                            member _.Handle = handler.Value.Handle },
                        steps
                    ))
        |> DSL.Seq.resultOrError

    innerLoop None tasks handlers

let rec private runTask getSchedule =
    fun (task: INodeHandle) (cTokens: CancellationToken list) ->
        async {
            let name = task.Name

            match
                cTokens
                |> List.tryPick (fun t -> if t.IsCancellationRequested then Some t else None)
            with
            | Some requestedToken ->
                $"Task '%s{name}'. Stopped by parent." |> Log.warning
                return [ requestedToken ]
            | None ->
                let cts = new CancellationTokenSource()

                match! getSchedule name with
                | Error error ->
                    cts.Cancel()
                    $"Task '%s{name}'. Failed: %s{error}" |> Log.error
                    return [ cts.Token ]
                | Ok schedule ->

                    let! taskExpirationToken = Scheduler.getExpirationToken task schedule cts

                    match taskExpirationToken.IsCancellationRequested with
                    | true ->
                        $"Task '%s{name}'. Stopped." |> Log.warning
                        return [ taskExpirationToken ]
                    | false ->

                        //$"Task '%s{name}'. Started." |> Log.trace

                        match task.Handle with
                        | None -> ()
                        | Some handle ->
                            match! handle () with
                            | Error error -> $"Task '%s{name}'. Failed: %s{error}" |> Log.error
                            | Ok msg -> $"Task '%s{name}'. Successful. %s{msg}" |> Log.success

                        let compleated = $"Task '%s{name}'. Completed."

                        match schedule with
                        | None -> compleated |> Log.trace
                        | Some schedule ->
                            match schedule.Delay with
                            | None -> compleated |> Log.trace
                            | Some delay ->
                                $"{compleated} Next task run will be in {delay}." |> Log.trace
                                do! Async.Sleep delay

                        return taskExpirationToken :: cTokens
        }

let start configure =
    async {

        try
            match! configure () with
            | Error error -> error |> Log.error
            | Ok config ->
                match config.Tasks |> merge <| config.Handlers with
                | Error error -> error |> Log.error
                | Ok tasks ->
                    let handleTask = runTask config.getSchedule

                    match! DSL.Graph.handleNodes tasks handleTask [] |> Async.Catch with
                    | Choice1Of2 _ -> $"All tasks completed successfully." |> Log.success
                    | Choice2Of2 ex ->
                        match ex with
                        | :? OperationCanceledException -> failwith "Worker was stopped."
                        | _ -> failwith $"Worker failed: %s{ex.Message}"
        with ex ->
            $"Worker failed: %s{ex.Message}" |> Log.error
    }
