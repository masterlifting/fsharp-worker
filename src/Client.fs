module Worker.Client

open System
open System.Threading
open Infrastructure.Domain
open Infrastructure.Prelude
open Infrastructure.Logging
open Worker.Domain
open Worker.Dependencies

let rec private handleNode nodeId attempt =
    fun (deps: WorkerTaskNode.Dependencies, schedule) ->
        async {
            match! deps.getNode nodeId with
            | Error error ->
                $"%i{attempt}.Task Id '%s{nodeId.Value}' Failed. Error: %s{error.Message}"
                |> Log.critical
            | Ok node ->

                let! schedule = node.Value |> deps.handleNode attempt schedule

                if schedule.IsSome && node.Children.Length > 0 then
                    do! (deps, schedule) |> handleNodes node.Children attempt

                if node.Value.Recursively.IsSome then
                    do! (deps, schedule) |> handleNode nodeId (attempt + 1u<attempts>)
        }

and private handleNodes nodes attempt =
    fun (deps, schedule) ->
        async {
            if nodes.Length > 0 then

                let nodeHandlers, skipLength =

                    let parallelNodes = nodes |> List.takeWhile _.Value.Parallel

                    match parallelNodes with
                    | parallelNodes when parallelNodes.Length < 2 ->

                        let sequentialNodes =
                            nodes |> List.skip 1 |> List.takeWhile (not << _.Value.Parallel)

                        let tasks =
                            nodes[0] :: sequentialNodes
                            |> List.map (fun task -> (deps, schedule) |> handleNode task.Id attempt)
                            |> Async.Sequential

                        (tasks, sequentialNodes.Length + 1)

                    | parallelNodes ->

                        let tasks =
                            parallelNodes
                            |> List.map (fun task -> (deps, schedule) |> handleNode task.Id attempt)
                            |> Async.Parallel

                        (tasks, parallelNodes.Length)

                do! nodeHandlers |> Async.Ignore

                let nodes = nodes |> List.skip skipLength

                do! (deps, schedule) |> handleNodes nodes attempt
        }

let private startTask taskName (deps: FireAndForget.Dependencies) =
    async {
        $"%s{taskName} Started." |> Log.debug

        use cts = new CancellationTokenSource(deps.Duration)

        match! deps.startHandler (deps.Task, deps.Configuration, cts.Token) with
        | Error error -> $"%s{taskName} Failed. Error: %s{error.Message}" |> Log.critical
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
        | Some startHandler ->
            let handler =
                startTask taskName {
                    Task = task.toWorkerTask schedule
                    Duration = task.Duration
                    Configuration = configuration
                    startHandler = startHandler
                }

            match task.Wait with
            | true -> do! handler
            | false -> Async.Start handler

        match task.Recursively with
        | Some delay ->
            $"%s{taskName} Next iteration will be started in %s{delay |> String.fromTimeSpan}."
            |> Log.trace

            do! Async.Sleep delay
        | None -> ()
    }

let private handleTask configuration =
    fun attempt parentSchedule (task: WorkerTaskNode) ->
        async {
            let taskName = $"%i{attempt}.'%s{task.Name}'"

            match Scheduler.set parentSchedule task.Schedule task.Recursively.IsSome with
            | Stopped reason ->
                $"%s{taskName} Stopped. %s{reason.Message}" |> Log.critical
                return None
            | StopIn(delay, schedule) ->
                if delay < TimeSpan.FromMinutes 10. then
                    $"%s{taskName} Will be stopped in %s{delay |> String.fromTimeSpan}."
                    |> Log.warning

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

let private processGraph nodeId deps =

    let nodeDeps: WorkerTaskNode.Dependencies = {
        getNode = deps.getTaskNode
        handleNode = handleTask deps.Configuration
    }

    (nodeDeps, None) |> handleNode nodeId 1u<attempts>

let start config =
    async {
        try
            let workerName = $"'%s{config.Name}'"

            match! processGraph config.TaskNodeRootId config |> Async.Catch with
            | Choice1Of2 _ -> $"%s{workerName} Completed." |> Log.success
            | Choice2Of2 ex ->
                match ex with
                | :? OperationCanceledException ->
                    let message = $"%s{workerName} Canceled."
                    failwith message
                | _ -> failwith $"%s{workerName} Failed. Error: %s{ex.Message}"
        with ex ->
            ex.Message |> Log.critical

        // Wait for the logger to finish writing logs
        do! Async.Sleep 1000
    }

let rec registerHandler nodeId handlerId handler =
    fun (graph: Graph.Node<WorkerTaskNode>) ->
        graph
        |> Graph.DFS.tryFindById nodeId
        |> Option.map (fun node ->
            Graph.Node(
                {
                    Id = node.Id
                    Name = node.Name
                    Handler =
                        node.Id
                        |> Graph.Node.Id.split
                        |> Seq.tryLast
                        |> Option.bind (fun id ->
                            match id.Value = handlerId with
                            | true -> handler |> Some
                            | false -> None)
                },
                node.Children
                |> List.map (fun child -> child |> registerHandler child.Id handlerId handler)
            ))
        |> Option.defaultValue (
            Graph.Node(
                {
                    Id = graph.Id
                    Name = graph.Name
                    Handler = None
                },
                []
            )
        )
