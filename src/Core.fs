module Worker.Core

open System
open System.Threading
open Microsoft.Extensions.Configuration
open Infrastructure
open Infrastructure.Domain.Graph
open Worker.Domain.Internal

let rec private handleNode
    nodeName
    (getNode: string -> Async<Result<Node<Task>, Error'>>)
    handleNodeValue
    configuration
    ct
    count
    =
    async {
        let count = count + uint 1

        match! getNode nodeName with
        | Error error -> $"Task '%s{nodeName}'. Failed: %s{error.Message}" |> Log.error
        | Ok node ->
            let nodeValue = { node.Value with Name = nodeName }

            let! ct = handleNodeValue configuration nodeValue ct count
            do! handleNodes nodeName node.Children getNode handleNodeValue configuration ct

            if nodeValue.Recursively && ct |> notCanceled then
                do! handleNode nodeName getNode handleNodeValue configuration ct count
    }

and handleNodes nodeName nodes getNode handleNodeValue configuration ct =
    async {
        if nodes.Length > 0 then
            let nodeHandlers, skipLength =

                let parallelNodes = nodes |> List.takeWhile (_.Value.Parallel)

                match parallelNodes with
                | parallelNodes when parallelNodes.Length < 2 ->

                    let sequentialNodes =
                        nodes |> List.skip 1 |> List.takeWhile (_.Value.Parallel >> not)

                    let tasks =
                        [ nodes[0] ] @ sequentialNodes
                        |> List.map (fun task ->
                            let nodeName = Some nodeName |> Graph.buildNodeName <| task.Value.Name
                            handleNode nodeName getNode handleNodeValue configuration ct 0u)
                        |> Async.Sequential

                    (tasks, sequentialNodes.Length + 1)

                | parallelNodes ->

                    let tasks =
                        parallelNodes
                        |> List.map (fun task ->
                            let nodeName = Some nodeName |> Graph.buildNodeName <| task.Value.Name
                            handleNode nodeName getNode handleNodeValue configuration ct 0u)
                        |> Async.Parallel

                    (tasks, parallelNodes.Length)

            do! nodeHandlers |> Async.Ignore
            do! handleNodes nodeName (nodes |> List.skip skipLength) getNode handleNodeValue configuration ct
    }

let private fireAndForget taskName (duration: TimeSpan option) configuration (handle: IConfigurationRoot -> CancellationToken -> Async<Result<TaskResult, Error'>>)  =
    async {
        $"{taskName} Started." |> Log.trace
        
        use cts = 
            match duration with
            | Some duration -> new CancellationTokenSource(duration)
            | None -> new CancellationTokenSource()

        match! handle configuration cts.Token with
        | Error error -> $"{taskName} Failed. %s{error.Message}" |> Log.error
        | Ok result ->
            let message = $"{taskName} Completed. "

            match result with
            | Success result -> $"{message}%A{result}" |> Log.success
            | Warn msg -> $"{message}%s{msg}" |> Log.warning
            | Debug msg -> $"{message}%s{msg}" |> Log.debug
            | Info msg -> $"{message}%s{msg}" |> Log.info
            | Trace msg -> $"{message}%s{msg}" |> Log.trace
    } |> Async.Start

let rec private handleTask configuration =
    fun (task: Task) parentToken count ->
        async {
            use cts = new CancellationTokenSource()

            let! taskToken = Scheduler.getExpirationToken task count cts

            use linkedCts = CancellationTokenSource.CreateLinkedTokenSource(parentToken, taskToken)

            let taskName = $"Task '%s{task.Name}'."

            match linkedCts.IsCancellationRequested with
            | true ->
                $"{taskName} Canceled." |> Log.warning
                return linkedCts.Token
            | false ->

                match task.Handle with
                | None -> $"{taskName} Skipped." |> Log.trace
                | Some handle -> handle |> fireAndForget taskName task.Duration configuration

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

            match! configuration |> processGraph rootName getTaskNode |> Async.Catch with
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
