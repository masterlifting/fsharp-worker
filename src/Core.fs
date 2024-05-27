module Worker.Core

open System
open Domain
open Domain.Core
open Infrastructure.DSL
open Infrastructure.Logging
open Infrastructure.Domain.Graph
open Infrastructure.Domain.Errors
open Infrastructure.DSL.Threading
open Worker
open System.Threading

let rec private handleNode
    nodeName
    (getNode: string -> Async<Result<Node<Task>, InfrastructureError>>)
    (handleNodeValue: Task -> CancellationToken -> uint -> Async<CancellationToken>)
    (cToken: CancellationToken)
    count
    =
    async {
        let count = count + uint 1

        match! getNode nodeName with
        | Error error -> $"Task '%s{nodeName}'. Failed: %s{error.Message}" |> Log.error
        | Ok node ->

            let! cToken = handleNodeValue node.Value cToken count
            do! handleNodes nodeName node.Children getNode handleNodeValue cToken

            if node.Value.Recursively && cToken |> notCanceled then
                do! handleNode nodeName getNode handleNodeValue cToken count
    }

and handleNodes nodeName nodes getNode handleNodeValue cToken =
    async {
        if nodes.Length > 0 then
            let nodeHandlers, skipLength =

                let parallelNodes = nodes |> List.takeWhile (_.Value.Parallel)

                match parallelNodes with
                | parallelNodes when parallelNodes.Length < 2 ->

                    let sequentialNodes =
                        nodes |> List.skip 1 |> List.takeWhile (fun node -> not node.Value.Parallel)

                    let tasks =
                        [ nodes[0] ] @ sequentialNodes
                        |> List.map (fun task ->
                            let nodeName = Some nodeName |> Graph.buildNodeName <| task.Value.Name
                            handleNode nodeName getNode handleNodeValue cToken 0u)
                        |> Async.Sequential

                    (tasks, sequentialNodes.Length + 1)

                | parallelNodes ->

                    let tasks =
                        parallelNodes
                        |> List.map (fun task ->
                            let nodeName = Some nodeName |> Graph.buildNodeName <| task.Value.Name
                            handleNode nodeName getNode handleNodeValue cToken 0u)
                        |> Async.Parallel

                    (tasks, parallelNodes.Length)

            do! nodeHandlers |> Async.Ignore
            do! handleNodes nodeName (nodes |> List.skip skipLength) getNode handleNodeValue cToken
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

let private processGraph nodeName getNode =
    handleNode nodeName getNode handleTask CancellationToken.None 0u

let start config =
    async {
        try
            let workerName = config.RootName

            match! processGraph workerName config.getTaskNode |> Async.Catch with
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
