module Worker.Client

open System
open System.Threading
open Infrastructure.Domain
open Infrastructure.Prelude
open Infrastructure.Prelude.Tree.Builder
open Infrastructure.Logging
open Persistence
open Persistence.Storages
open Persistence.Storages.Domain
open Worker.Domain
open Worker.DataAccess
open Worker.Dependencies

let private resultAsync = ResultAsyncBuilder()

let rec internal processTask taskId attempt =
    fun (deps: WorkerTask.Dependencies<_>, schedule) ->
        async {
            match! deps.findTask taskId with
            | Error error -> $"%i{attempt}. Task Id '{taskId}' Failed. Error: %s{error.Message}" |> Log.crt
            | Ok None -> $"%i{attempt}. Task Id '{taskId}' not found." |> Log.crt
            | Ok(Some task) ->

                let! schedule = task.Value |> deps.tryStartTask attempt schedule

                if schedule.IsSome && not (task.Children |> Seq.isEmpty) then
                    do! (deps, schedule) |> processTasks (task.Children |> List.ofSeq) attempt

                if task.Value.Schedule |> Option.bind _.Recursively |> Option.isSome then
                    do! (deps, schedule) |> processTask task.Value.Id (attempt + 1u<attempts>)
        }

and internal processTasks tasks attempt =
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
                            |> List.map (fun task -> (deps, schedule) |> processTask task.Value.Id attempt)
                            |> Async.Sequential

                        asyncTasks, sequentialTasks.Length + 1

                    | parallelTasks ->

                        let asyncTasks =
                            parallelTasks
                            |> List.map (fun task -> (deps, schedule) |> processTask task.Value.Id attempt)
                            |> Async.Parallel

                        asyncTasks, parallelTasks.Length

                do! asyncTasks |> Async.Ignore
                let nextTasks = tasks |> List.skip skipLength
                do! (deps, schedule) |> processTasks nextTasks attempt
        }

let internal startTask attempt (task: WorkerTask<_>) =
    fun (schedule, taskDeps) ->

        let taskName = task.Print attempt

        let inline start (deps: FireAndForget.Dependencies<_>) =
            async {
                $"%s{taskName} Started." |> Log.dbg

                use cts = new CancellationTokenSource(deps.Duration)

                match! deps.startActiveTask (deps.ActiveTask, deps.TaskDeps, cts.Token) with
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
                        startActiveTask = startActiveTask
                        TaskDeps = taskDeps
                    }

                match task.WaitResult with
                | true -> do! handler
                | false -> Async.Start handler

            match task.Schedule |> Option.bind _.Recursively with
            | Some delay ->
                $"%s{taskName} Next iteration will be started in %s{delay |> TimeSpan.print}."
                |> Log.trc

                do! Async.Sleep delay
            | None -> ()
        }

let internal tryStartTask taskDeps =
    fun attempt parentSchedule (task: WorkerTask<_>) ->
        async {
            let taskName = task.Print attempt

            let inline tryStartTask schedule task =
                (schedule, taskDeps) |> startTask attempt task

            match Scheduler.set parentSchedule task.Schedule with
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

let internal merge (handlers: Tree.Node<WorkerTaskHandler<_>>) =
    fun (tasks: Tree.Node<TaskNode>) ->

        let rec toWorkerTaskNode (node: Tree.Node<TaskNode>) =

            let workerTask =
                Tree.Node.create (
                    node.Id.CurrentValue,
                    {
                        Id = WorkerTaskId.create node.Id.Value
                        Description = node.Value.Description
                        Parallel = node.Value.Parallel
                        Duration = node.Value.Duration
                        WaitResult = node.Value.WaitResult
                        Schedule = node.Value.Schedule
                        Handler =
                            match node.Value.Enabled with
                            | true -> handlers |> Tree.findNode node.Id |> Option.bind _.Value
                            | false -> None
                    }
                )

            match node.Children |> Seq.isEmpty with
            | true -> workerTask |> Ok
            | false ->
                node.Children
                |> Seq.map toWorkerTaskNode
                |> Result.choose
                |> Result.map (fun children -> workerTask |> withChildren children)

        tasks |> toWorkerTaskNode

let internal findTask (taskId: WorkerTaskId) (handlers: Tree.Node<WorkerTaskHandler<_>>) =
    fun storage ->
        match storage with
        | Storage.Database database ->
            match database with
            | Database.Client.Postgre client ->
                client
                |> Postgre.Provider.clone
                |> Postgre.TasksTree.Query.findById taskId
                |> ResultAsync.bind (Option.toResult (merge handlers))
                |> ResultAsync.apply (fun _ -> client |> Postgre.Provider.dispose |> Ok)
        | Storage.Configuration client ->
            client
            |> Configuration.TasksTree.Query.get
            |> ResultAsync.map (Tree.findNode taskId.NodeId)
            |> ResultAsync.bind (Option.toResult (merge handlers))
        | Storage.FileSystem _
        | Storage.InMemory _ -> $"The '{storage}' is not supported." |> NotSupported |> Error |> async.Return

let internal initialize tasks storage =
    match storage with
    | Storage.Database database ->
        match database with
        | Database.Client.Postgre client ->
            resultAsync {
                do! client |> Postgre.Schedule.Migrations.apply
                do! client |> Postgre.TasksTree.Migrations.apply

                return
                    match tasks with
                    | None -> Ok() |> async.Return
                    | Some tasks -> client |> Postgre.TasksTree.Command.insert tasks
            }
    | Storage.Configuration client -> client |> Configuration.TasksTree.Query.get |> ResultAsync.map ignore
    | Storage.FileSystem _
    | Storage.InMemory _ ->
        $"'{storage}' storage is not supported."
        |> NotSupported
        |> Error
        |> async.Return

let start (deps: Worker.Dependencies<'a>) =
    async {
        try
            let workerName = $"'%s{deps.Name}'."

            Log.inf $"%s{workerName} Initializing storage..."

            match deps.Storage |> Storage.init with
            | Error error -> failwith $"%s{workerName} Storage initialization failed. Error: %s{error.Message}"
            | Ok storage ->

                match! storage |> initialize deps.Tasks with
                | Error error -> failwith $"%s{workerName} Storage initialization failed. Error: %s{error.Message}"
                | Ok() -> Log.inf $"%s{workerName} Storage initialized."

                let taskDeps: WorkerTask.Dependencies<'a> = {
                    findTask = fun taskId -> storage |> findTask taskId deps.Handlers
                    tryStartTask = tryStartTask deps.TaskDeps
                }

                let rootTaskId = deps.RootTaskId |> WorkerTaskId.create

                match! (taskDeps, None) |> processTask rootTaskId 1u<attempts> |> Async.Catch with
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
