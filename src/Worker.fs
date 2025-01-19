[<RequireQualifiedAccess>]
module Worker.Worker

open System
open System.Threading
open Infrastructure.Prelude
open Infrastructure.Logging
open Worker.Domain
open Worker.Dependencies

let rec private handleNode (nodeId, count, schedule) (deps: WorkerTaskNode.Dependencies) =
    async {
        match! deps.getNode nodeId with
        | Error error -> $"%i{count}.Task Id '%s{nodeId.Value}' Failed -> %s{error.Message}" |> Log.critical
        | Ok node ->

            let! schedule = node.Value |> deps.handleNode count schedule
            do! node.Children |> handleNodes (count, deps, schedule)

            if node.Value.Recursively.IsSome then
                let count = count + 1u
                do! handleNode (nodeId, count, schedule) deps
    }

and private handleNodes (count, deps, schedule) nodes =
    async {
        if nodes.Length > 0 then

            let nodeHandlers, skipLength =

                let parallelNodes = nodes |> List.takeWhile _.Value.Parallel

                match parallelNodes with
                | parallelNodes when parallelNodes.Length < 2 ->

                    let sequentialNodes =
                        nodes |> List.skip 1 |> List.takeWhile (not << _.Value.Parallel)

                    let tasks =
                        [ nodes[0] ] @ sequentialNodes
                        |> List.map (fun task -> deps |> handleNode (task.Id, count, schedule))
                        |> Async.Sequential

                    (tasks, sequentialNodes.Length + 1)

                | parallelNodes ->

                    let tasks =
                        parallelNodes
                        |> List.map (fun task -> deps |> handleNode (task.Id, count, schedule))
                        |> Async.Parallel

                    (tasks, parallelNodes.Length)

            do! nodeHandlers |> Async.Ignore

            do! nodes |> List.skip skipLength |> handleNodes (count, deps, schedule)
    }

let private runHandler taskName (deps: FireAndForget.Dependencies) =
    async {
        $"%s{taskName} Started." |> Log.debug

        use cts = new CancellationTokenSource(deps.Duration)

        match! deps.startHandler (deps.Task, deps.Configuration, cts.Token) with
        | Error error -> $"%s{taskName} Failed -> %s{error.Message}" |> Log.critical
        | Ok result ->
            let message = $"%s{taskName} Completed. "

            match result with
            | Success result -> $"%s{message}%A{result}" |> Log.success
            | Warn msg -> $"%s{message}%s{msg}" |> Log.warning
            | Debug msg -> $"%s{message}%s{msg}" |> Log.debug
            | Info msg -> $"%s{message}%s{msg}" |> Log.info
            | Trace msg -> $"%s{message}%s{msg}" |> Log.trace
    }

let private tryStart taskName schedule configuration (task: WorkerTaskNode) =
    async {
        match task.Handler with
        | None -> $"%s{taskName} Skipped." |> Log.trace
        | Some handler ->
            let run =
                runHandler
                    taskName
                    { Task = task.toWorkerTask schedule
                      Duration = task.Duration
                      Configuration = configuration
                      startHandler = handler }

            if task.Wait then do! run else run |> Async.Start

        match task.Recursively with
        | Some delay ->
            $"%s{taskName} Next iteration will be started in %s{delay |> String.fromTimeSpan}."
            |> Log.trace

            do! Async.Sleep delay
        | None -> ()
    }

let rec private handleTask configuration =
    fun count parentSchedule (task: WorkerTaskNode) ->
        async {
            let taskName = $"%i{count}.'%s{task.Name}'"

            match Scheduler.set parentSchedule task.Schedule task.Recursively.IsSome with
            | Stopped(reason, schedule) ->
                $"%s{taskName} Stopped. %s{reason.Message}" |> Log.critical
                return Some schedule
            | StopIn(delay, schedule) ->
                if (delay < TimeSpan.FromMinutes 10.) then
                    $"%s{taskName} Will be stopped in %s{delay |> String.fromTimeSpan}."
                    |> Log.debug

                do! task |> tryStart taskName schedule configuration
                return Some schedule
            | StartIn(delay, schedule) ->
                $"%s{taskName} Will be started in %s{delay |> String.fromTimeSpan}."
                |> Log.warning

                do! Async.Sleep delay
                do! task |> tryStart taskName schedule configuration
                return Some schedule
            | Started schedule ->
                do! task |> tryStart taskName schedule configuration
                return Some schedule
            | NotScheduled ->
                if task.Handler.IsSome then
                    $"%s{taskName} Handling was skipped due to the schedule was not found."
                    |> Log.warning

                return None
        }

let private processGraph nodeName deps =
    handleNode
        (nodeName, 1u, None)
        { getNode = deps.getTaskNode
          handleNode = handleTask <| deps.Configuration }

let start config =
    async {
        try
            let workerName = $"'%s{config.RootNodeName}'"

            match! processGraph config.RootNodeId config |> Async.Catch with
            | Choice1Of2 _ -> $"%s{workerName} Completed." |> Log.success
            | Choice2Of2 ex ->
                match ex with
                | :? OperationCanceledException ->
                    let message = $"%s{workerName} Canceled."
                    failwith message
                | _ -> failwith $"%s{workerName} Failed -> %s{ex.Message}"
        with ex ->
            ex.Message |> Log.critical

        // Wait for the logger to finish writing logs
        do! Async.Sleep 1000
    }
