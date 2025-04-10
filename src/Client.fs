module Worker.Client

open System
open System.Threading
open Infrastructure.Domain
open Infrastructure.Prelude
open Infrastructure.Logging
open Worker.Domain
open Worker.Dependencies

let rec private handleNode nodeId attempt =
    fun (deps: WorkerTask.Dependencies, schedule) ->
        async {
            match! deps.tryFindNode nodeId with
            | Error error ->
                $"%i{attempt}.Task Id '%s{nodeId.Value}' Failed. Error: %s{error.Message}"
                |> Log.crt
            | Ok None -> $"%i{attempt}.Task Id '%s{nodeId.Value}' not Found." |> Log.crt
            | Ok(Some node) ->

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
        $"%s{taskName} Started." |> Log.dbg

        use cts = new CancellationTokenSource(deps.Duration)

        match! deps.startHandler (deps.ActiveTask, deps.Configuration, cts.Token) with
        | Error error -> $"%s{taskName} Failed. Error: %s{error.Message}" |> Log.crt
        | Ok() -> $"%s{taskName} Completed." |> Log.inf
    }

let private tryStart (task: WorkerTask) =
    fun (name, attempt, schedule, configuration) ->
        async {
            match task.Handler with
            | None -> $"%s{name} Skipped." |> Log.trc
            | Some startHandler ->
                let handler =
                    startTask name {
                        ActiveTask = task.ToActiveTask schedule attempt
                        Duration = task.Duration
                        Configuration = configuration
                        startHandler = startHandler
                    }

                match task.WaitResult with
                | true -> do! handler
                | false -> Async.Start handler

            match task.Recursively with
            | Some delay ->
                $"%s{name} Next iteration will be started in %s{delay |> String.fromTimeSpan}."
                |> Log.trc

                do! Async.Sleep delay
            | None -> ()
        }

let private handleTask configuration =
    fun attempt parentSchedule (task: WorkerTask) ->
        async {
            let taskName = $"%i{attempt}.'%s{task.Id.Value}'"

            let inline tryStart schedule task =
                (taskName, attempt, schedule, configuration) |> tryStart task

            match Scheduler.set parentSchedule task.Schedule task.Recursively.IsSome with
            | Stopped reason ->
                $"%s{taskName} Stopped. %s{reason.Message}" |> Log.crt
                return None
            | StopIn(delay, schedule) ->
                if delay < TimeSpan.FromMinutes 10. then
                    $"%s{taskName} Will be stopped in %s{delay |> String.fromTimeSpan}." |> Log.wrn

                do! task |> tryStart schedule
                return Some schedule
            | StartIn(delay, schedule) ->
                $"%s{taskName} Will be started in %s{delay |> String.fromTimeSpan}." |> Log.wrn

                do! Async.Sleep delay
                do! task |> tryStart schedule
                return Some schedule
            | Started schedule ->
                do! task |> tryStart schedule
                return Some schedule
            | NotScheduled ->
                if task.Handler.IsSome then
                    $"%s{taskName} Handling was skipped due to the schedule was not found."
                    |> Log.wrn

                return None
        }

let private processGraph nodeId deps =

    let nodeDeps: WorkerTask.Dependencies = {
        tryFindNode = deps.tryFindTask
        handleNode = handleTask deps.Configuration
    }

    (nodeDeps, None) |> handleNode nodeId 1u<attempts>

let start config =
    async {
        try
            let workerName = $"'%s{config.Name}'"

            match! processGraph config.TaskNodeRootId config |> Async.Catch with
            | Choice1Of2 _ -> $"%s{workerName} Completed." |> Log.scs
            | Choice2Of2 ex ->
                match ex with
                | :? OperationCanceledException ->
                    let message = $"%s{workerName} Canceled."
                    failwith message
                | _ -> failwith $"%s{workerName} Failed. Error: %s{ex.Message}"
        with ex ->
            ex.Message |> Log.crt

        // Wait for the logger to finish writing logs
        do! Async.Sleep 1000
    }

let createHandlers nodeId (handlers: WorkerTaskHandler seq) =
    fun (graph: Graph.Node<TaskGraph>) ->

        let rec innerLoop (node: Graph.Node<TaskGraph>) =
            Graph.Node(
                {
                    Id = node.ShortId
                    Handler =
                        handlers
                        |> Seq.tryFind (fun handler -> handler.Id = node.ShortId)
                        |> Option.bind _.Handler
                },
                node.Children |> List.map innerLoop
            )

        graph |> Graph.DFS.tryFindById nodeId |> Option.map innerLoop

let registerHandlers (handlers: Graph.Node<WorkerTaskHandler>) =
    fun (taskGraph: Graph.Node<TaskGraph>) ->

        let rec innerLoop (graph: Graph.Node<TaskGraph>) =
            let node = {
                Id = graph.ShortId
                Description = graph.Value.Description
                Parallel = graph.Value.Parallel
                Recursively = graph.Value.Recursively
                Duration = graph.Value.Duration
                WaitResult = graph.Value.WaitResult
                Schedule = graph.Value.Schedule
                Handler =
                    match graph.Value.Enabled with
                    | false -> None
                    | true -> handlers |> Graph.BFS.tryFindById graph.Id |> Option.bind _.Value.Handler
            }

            match graph.Children.Length = 0 with
            | true -> Graph.Node(node, [])
            | false ->
                graph.Children
                |> List.map innerLoop
                |> fun children -> Graph.Node(node, children)

        taskGraph |> innerLoop
