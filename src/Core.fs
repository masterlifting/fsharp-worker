module Worker.Core

open System
open Domain
open Domain.Core
open Infrastructure
open Infrastructure.Logging
open Infrastructure.Domain.Graph
open Infrastructure.Domain.Errors
open Infrastructure.DSL.Threading
open Worker
open System.Threading

let private merge tasks handlers =

    let rec innerLoop nodeName (tasks: Node<Task> list) (handlers: Node<TaskHandler> list) =
        tasks
        |> List.map (fun task ->
            let fillNodeName = nodeName |> DSL.Graph.buildNodeName <| task.Value.Name

            match handlers |> List.tryFind (fun handler -> handler.Value.Name = task.Value.Name) with
            | None -> Error $"Task %s{fillNodeName}. Failed: Handler was not found."
            | Some handler ->

                match innerLoop (Some fillNodeName) task.Children handler.Children with
                | Error error -> Error error
                | Ok steps ->

                    if handler.Value.Handle.IsNone then
                        $"Task '%s{fillNodeName}'. Handling function was not set." |> Log.warning

                    Ok
                    <| Node(
                        { task.Value with
                            Name = fillNodeName
                            Handle = handler.Value.Handle },
                        steps
                    ))
        |> DSL.Seq.resultOrError

    innerLoop None tasks handlers

let rec handleNodes
    (nodes: Node<Task> list)
    (getTaskNode: string -> Async<Result<Node<Task>, InfrastructureError>>)
    (handleTask: Task -> CancellationToken -> uint -> Async<CancellationToken>)
    (cToken: CancellationToken)
    =
    async {
        if nodes.Length > 0 then
            let tasks, skipLength =

                let parallelNodes = nodes |> List.takeWhile (_.Value.Parallel)

                match parallelNodes with
                | parallelNodes when parallelNodes.Length < 2 ->

                    let sequentialNodes =
                        nodes |> List.skip 1 |> List.takeWhile (fun node -> not node.Value.Parallel)

                    let tasks =
                        [ nodes[0] ] @ sequentialNodes
                        |> List.map (fun node -> handleNode node getTaskNode handleTask cToken 0u)
                        |> Async.Sequential

                    (tasks, sequentialNodes.Length + 1)

                | parallelNodes ->

                    let tasks =
                        parallelNodes
                        |> List.map (fun node -> handleNode node getTaskNode handleTask cToken 0u)
                        |> Async.Parallel

                    (tasks, parallelNodes.Length)

            do! tasks |> Async.Ignore
            do! handleNodes (nodes |> List.skip skipLength) getTaskNode handleTask cToken
    }

and handleNode node getNode handleValue cToken count =
    async {
        let count = count + uint 1

        match! getNode node.Value.Name with
        | Error error ->
            $"Task '%s{node.Value.Name}'. Failed: %s{error.Message}" |> Log.error
            let cts = new CancellationTokenSource()
            do! handleNodes node.Children getNode handleValue cts.Token
        | Ok taskNode ->

            let! cToken = handleValue taskNode.Value cToken count
            do! handleNodes node.Children getNode handleValue cToken

            if taskNode.Value.Recursively && cToken |> notCanceled then
                do! handleNode taskNode getNode handleValue cToken count
    }


let rec private handleTask =
    fun (task: Task) parentToken count ->
        async {
            let taskName = $"Task '%s{task.Name}'."

            if parentToken |> canceled then
                $"{taskName} Canceled by parent." |> Log.warning
                return parentToken
            else
                use cts = new CancellationTokenSource()

                let! taskToken = Scheduler.getExpirationToken task count cts

                use linkedCts =
                    CancellationTokenSource.CreateLinkedTokenSource(parentToken, taskToken)

                match linkedCts.IsCancellationRequested with
                | true ->
                    $"{taskName} Canceled." |> Log.warning
                    return linkedCts.Token
                | false ->

                    match task.Handle with
                    | None -> ()
                    | Some handle ->
                        $"{taskName} Started." |> Log.trace

                        use cts = new CancellationTokenSource()

                        if task.Duration.IsSome then
                            cts.CancelAfter task.Duration.Value

                        match! handle cts.Token with
                        | Error error -> $"{taskName} Failed: %s{error.Message}" |> Log.error
                        | Ok msg -> $"{taskName} Success. %s{msg}" |> Log.success

                    let completed = $"{taskName} Completed."

                    match task.Schedule with
                    | None -> completed |> Log.debug
                    | Some schedule ->
                        match schedule.Delay with
                        | None -> completed |> Log.debug
                        | Some delay ->
                            $"%s{completed} Next task will be run in {delay}." |> Log.debug
                            do! Async.Sleep delay

                    return linkedCts.Token
        }

let start config =
    async {
        try
            let workerName = config.TasksGraph.Value.Name

            $"Worker '%s{workerName}' started." |> Log.info

            match config.TasksGraph.Children |> merge <| config.Handlers with
            | Error error -> error |> Log.error
            | Ok tasks ->
                match!
                    handleNodes tasks config.getTaskNode handleTask CancellationToken.None
                    |> Async.Catch
                with
                | Choice1Of2 _ -> $"All tasks of the worker '%s{workerName}' were completed." |> Log.success
                | Choice2Of2 ex ->
                    match ex with
                    | :? OperationCanceledException ->
                        let message = $"Worker '%s{workerName}' was stopped."
                        failwith message
                    | _ -> failwith $"Worker '%s{workerName}' failed: %s{ex.Message}"
        with ex ->
            ex.Message |> Log.error
    }
