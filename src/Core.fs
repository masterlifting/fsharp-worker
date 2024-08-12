module Worker.Core

open System
open System.Threading
open Infrastructure
open Infrastructure.Logging
open Worker.Domain

let rec private handleNode count ct (deps: HandleNodeDeps)=
    async {
        let count = count + uint 1
        let taskName = deps.TaskName

        match! deps.getTask taskName with
        | Error error -> $"Task '%s{taskName}'. Failed: %s{error.Message}" |> Log.error
        | Ok node ->
            let task = { node.Value with Name = taskName }

            let! ct = task |> deps.handleTask count ct
            do! node.Children |> handleNodes deps ct

            if task.Recursively && ct |> notCanceled then
                do! handleNode count ct deps
    }

and handleNodes deps ct nodes=
    async {
        if nodes.Length > 0 then
            
            let nodeName = deps.TaskName
            
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
                            { deps with TaskName = nodeName } |> handleNode 0u ct)
                        |> Async.Sequential

                    (tasks, sequentialNodes.Length + 1)

                | parallelNodes ->

                    let tasks =
                        parallelNodes
                        |> List.map (fun task ->
                            let nodeName = Some nodeName |> Graph.buildNodeName <| task.Value.Name
                            { deps with TaskName = nodeName } |> handleNode 0u ct)
                        |> Async.Parallel

                    (tasks, parallelNodes.Length)

            do! nodeHandlers |> Async.Ignore
            
            do! nodes |> List.skip skipLength |> handleNodes deps ct
    }

let private fireAndForget deps taskName  =
    async {
        $"{taskName} Started." |> Log.trace
        
        use cts = 
            match deps.Duration with
            | Some duration -> new CancellationTokenSource(duration)
            | None -> new CancellationTokenSource()

        match! deps.handleTask (deps.Configuration, deps.Schedule,cts.Token) with
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
    fun count parentToken (task: Task) ->
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
                | Some handle -> 
                    taskName 
                    |> fireAndForget 
                        { Configuration = configuration
                          Duration = task.Duration
                          Schedule = task.Schedule
                          handleTask = handle}

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

let private processGraph nodeName (deps: WorkerDeps) =
    handleNode 0u CancellationToken.None
        { TaskName = nodeName
          getTask = deps.getTask
          handleTask = handleTask <| deps.Configuration }

let start deps name =
    async {
        try
            let workerName = $"Worker '%s{name}'."

            match! processGraph name deps |> Async.Catch with
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
