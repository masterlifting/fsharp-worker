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
            match! deps.findTask taskId with
            | Error error ->
                $"%i{attempt}.Task Id '%s{taskId}' Failed. Error: %s{error.Message}"
                |> Log.crt
            | Ok None -> $"%i{attempt}.Task Id '%s{taskId}' not Found." |> Log.crt
            | Ok(Some task) ->

                let! schedule = task.Value |> deps.tryStartTask attempt schedule

                if schedule.IsSome && not (task.Children |> Seq.isEmpty) then
                    do! (deps, schedule) |> processTasks (task.Children |> List.ofSeq) attempt

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
                findTask = deps.findTask
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

let merge (handlers: Tree.Node<WorkerTaskHandler>) =
    fun (tasks: Tree.Node<TaskNode>) ->

        let rec toWorkerTaskNode (node: Tree.Node<TaskNode>) =
            let task = {
                Id = node.Id
                Description = node.Value.Description
                Parallel = node.Value.Parallel
                Recursively = node.Value.Recursively
                Duration = node.Value.Duration
                WaitResult = node.Value.WaitResult
                Schedule = node.Value.Schedule
                Handler =
                    match node.Value.Enabled with
                    | false -> None
                    | true -> handlers.FindValue node.Id |> Option.map _.Value
            }

            let resultNode = Tree.Node.create(node.Id, task);

            match node.Children |> Seq.isEmpty with
            | true -> resultNode
            | false ->
                node.Children
                |> Seq.map toWorkerTaskNode
                |> fun children -> resultNode.AddChildren children

        tasks |> toWorkerTaskNode
