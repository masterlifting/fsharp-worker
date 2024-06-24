module Worker.Core

open System
open System.Threading
open Infrastructure.Dsl
open Infrastructure.Logging
open Infrastructure.Dsl.Threading
open Infrastructure.Domain.Graph
open Infrastructure.Domain.Errors
open Domain.Internal

let rec private handleNode
    nodeName
    (getNode: string -> Async<Result<Node<Task>, ErrorType>>)
    handleNodeValue
    configuration
    cToken
    count
    =
    async {
        let count = count + uint 1

        match! getNode nodeName with
        | Error error -> $"Task '%s{nodeName}'. Failed: %s{error.Message}" |> Log.error
        | Ok node ->
            let nodeValue = { node.Value with Name = nodeName }

            let! cToken = handleNodeValue configuration nodeValue cToken count
            do! handleNodes nodeName node.Children getNode handleNodeValue configuration cToken

            if nodeValue.Recursively && cToken |> notCanceled then
                do! handleNode nodeName getNode handleNodeValue configuration cToken count
    }

and handleNodes nodeName nodes getNode handleNodeValue configuration cToken =
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
                            handleNode nodeName getNode handleNodeValue configuration cToken 0u)
                        |> Async.Sequential

                    (tasks, sequentialNodes.Length + 1)

                | parallelNodes ->

                    let tasks =
                        parallelNodes
                        |> List.map (fun task ->
                            let nodeName = Some nodeName |> Graph.buildNodeName <| task.Value.Name
                            handleNode nodeName getNode handleNodeValue configuration cToken 0u)
                        |> Async.Parallel

                    (tasks, parallelNodes.Length)

            do! nodeHandlers |> Async.Ignore
            do! handleNodes nodeName (nodes |> List.skip skipLength) getNode handleNodeValue configuration cToken
    }

let rec private handleTask configuration =
    fun (task: Task) parentToken count ->
        async {
            let taskName = $"Task '%s{task.Name}'."

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
                | None -> $"{taskName} Skipped." |> Log.trace
                | Some handle ->
                    $"{taskName} Started." |> Log.trace

                    use cts = new CancellationTokenSource()

                    if task.Duration.IsSome then
                        cts.CancelAfter task.Duration.Value

                    match! handle configuration cts.Token with
                    | Error error -> $"{taskName} Failed: %s{error.Message}" |> Log.error
                    | Ok result ->
                        let message = $"{taskName} Completed. "

                        match result with
                        | Success msg -> $"{message}%A{msg}" |> Log.success
                        | Warn msg -> $"{message}%s{msg}" |> Log.warning
                        | Debug msg -> $"{message}%s{msg}" |> Log.debug
                        | Info msg -> $"{message}%s{msg}" |> Log.info
                        | Trace msg -> $"{message}%s{msg}" |> Log.trace

                match task.Schedule with
                | None -> ()
                | Some schedule ->
                    match schedule.Delay with
                    | None -> ()
                    | Some delay ->
                        $"{taskName} Next task will be run in {delay}." |> Log.debug
                        do! Async.Sleep delay

                return linkedCts.Token
        }

let private processGraph nodeName getNode configuration =
    handleNode nodeName getNode handleTask configuration CancellationToken.None 0u

let start rootName getTaskNode configuration =
    async {
        try
            let workerName = $"Worker '%s{rootName}'."

            match!  configuration |> processGraph rootName getTaskNode |> Async.Catch with
            | Choice1Of2 _ -> $"{workerName} Completed." |> Log.success
            | Choice2Of2 ex ->
                match ex with
                | :? OperationCanceledException ->
                    let message = $"{workerName} Canceled."
                    failwith message
                | _ -> failwith $"{workerName} Failed: %s{ex.Message}"
        with ex ->
            ex.Message |> Log.error
    }
