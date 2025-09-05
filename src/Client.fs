module Worker.Client

open System
open System.Threading
open Infrastructure.Domain
open Infrastructure.Prelude
open Infrastructure.Logging
open Worker.Domain
open Worker.Dependencies

let rec private processTask taskId attempt =
    fun (deps: WorkerTask.Dependencies, schedule) ->
        async {
            match! deps.tryFindTask taskId with
            | Error error ->
                $"%i{attempt}.Task Id '%s{taskId.Value}' Failed. Error: %s{error.Message}"
                |> Log.crt
            | Ok None -> $"%i{attempt}.Task Id '%s{taskId.Value}' not Found." |> Log.crt
            | Ok(Some task) ->

                let! schedule = task.Value |> deps.tryStartTask attempt schedule

                if schedule.IsSome && task.Children.Length > 0 then
                    do! (deps, schedule) |> processTasks task.Children attempt

                if task.Value.Recursively.IsSome then
                    do! (deps, schedule) |> processTask taskId (attempt + 1u<attempts>)
        }

and private processTasks tasks attempt =
    fun (deps, schedule) ->
        async {
            if tasks.Length > 0 then
                let asyncTasks, skipLength =
                    match tasks |> List.takeWhile _.Value.Parallel with
                    | parallelTasks when parallelTasks.Length < 2 ->

                        let sequentialTasks =
                            tasks |> List.skip 1 |> List.takeWhile (not << _.Value.Parallel)

                        let asyncTasks =
                            tasks[0] :: sequentialTasks
                            |> List.map (fun task -> (deps, schedule) |> processTask task.Id attempt)
                            |> Async.Sequential

                        asyncTasks, sequentialTasks.Length + 1

                    | parallelTasks ->

                        let asyncTasks =
                            parallelTasks
                            |> List.map (fun task -> (deps, schedule) |> processTask task.Id attempt)
                            |> Async.Parallel

                        asyncTasks, parallelTasks.Length

                do! asyncTasks |> Async.Ignore
                let nextTasks = tasks |> List.skip skipLength
                do! (deps, schedule) |> processTasks nextTasks attempt
        }

let private startTask attempt (task: WorkerTask) =
    fun (schedule, configuration) ->

        let taskName = task.Print attempt

        let inline start (deps: FireAndForget.Dependencies) =
            async {
                $"%s{taskName} Started." |> Log.dbg

                use cts = new CancellationTokenSource(deps.Duration)

                match! deps.startActiveTask (deps.ActiveTask, deps.Configuration, cts.Token) with
                | Error error -> $"%s{taskName} Failed. Error: %s{error.Message}" |> Log.crt
                | Ok() -> $"%s{taskName} Completed." |> Log.inf
            }

        async {
            match task.Handler with
            | None -> $"%s{taskName} Skipped." |> Log.trc
            | Some startActiveTask ->
                let handler =
                    start {
                        ActiveTask = task.ToActiveTask schedule attempt
                        Duration = task.Duration
                        Configuration = configuration
                        startActiveTask = startActiveTask
                    }

                match task.WaitResult with
                | true -> do! handler
                | false -> Async.Start handler

            match task.Recursively with
            | Some delay ->
                $"%s{taskName} Next iteration will be started in %s{delay |> TimeSpan.print}."
                |> Log.trc

                do! Async.Sleep delay
            | None -> ()
        }

let private tryStartTask configuration =
    fun attempt parentSchedule (task: WorkerTask) ->
        async {
            let taskName = task.Print attempt

            let inline tryStartTask schedule task =
                (schedule, configuration) |> startTask attempt task

            match Scheduler.set parentSchedule task.Schedule task.Recursively.IsSome with
            | Stopped reason ->
                $"%s{taskName} Stopped. %s{reason.Message}" |> Log.crt
                return None
            | StopIn(delay, schedule) ->
                if delay < TimeSpan.FromMinutes 10. then
                    $"%s{taskName} Will be stopped in %s{delay |> TimeSpan.print}." |> Log.wrn

                do! task |> tryStartTask schedule
                return Some schedule
            | StartIn(delay, schedule) ->
                $"%s{taskName} Will be started in %s{delay |> TimeSpan.print}." |> Log.wrn

                do! Async.Sleep delay
                do! task |> tryStartTask schedule
                return Some schedule
            | Started schedule ->
                do! task |> tryStartTask schedule
                return Some schedule
            | NotScheduled ->
                if task.Handler.IsSome then
                    $"%s{taskName} Handling was skipped due to the schedule was not found."
                    |> Log.wrn

                return None
        }

let start (deps: Worker.Dependencies) =
    async {
        try
            let workerName = $"'%s{deps.Name}'."

            let taskDeps: WorkerTask.Dependencies = {
                tryFindTask = deps.tryFindTask
                tryStartTask = tryStartTask deps.Configuration
            }

            match! (taskDeps, None) |> processTask deps.RootTaskId 1u<attempts> |> Async.Catch with
            | Choice1Of2 _ -> $"%s{workerName} Stopped." |> Log.scs
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
    fun (tree: Tree.Node<TaskNode>) ->

        let rec innerLoop (node: Tree.Node<TaskNode>) =
            Tree.Node(
                {
                    Id = node.ShortId
                    Handler =
                        handlers
                        |> Seq.tryFind (fun handler -> handler.Id = node.ShortId)
                        |> Option.bind _.Handler
                },
                node.Children |> List.map innerLoop
            )

        tree |> Tree.DFS.tryFind nodeId |> Option.map innerLoop

let mapTasks (handlers: Tree.Node<WorkerTaskHandler>) =
    fun (tasksTree: Tree.Node<TaskNode>) ->

        let rec innerLoop (tree: Tree.Node<TaskNode>) =
            let node = {
                Id = tree.ShortId
                Description = tree.Value.Description
                Parallel = tree.Value.Parallel
                Recursively = tree.Value.Recursively
                Duration = tree.Value.Duration
                WaitResult = tree.Value.WaitResult
                Schedule = tree.Value.Schedule
                Handler =
                    match tree.Value.Enabled with
                    | false -> None
                    | true -> handlers |> Tree.BFS.tryFind tree.Id |> Option.bind _.Value.Handler
            }

            match tree.Children.Length = 0 with
            | true -> Tree.Node(node, [])
            | false ->
                tree.Children
                |> List.map innerLoop
                |> fun children -> Tree.Node(node, children)

        tasksTree |> innerLoop
