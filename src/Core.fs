module Worker.Core

open System
open System.Threading
open Domain
open Domain.Core
open Worker
open Infrastructure.Logging

let private handleTask name task =
    async {
        match task.Handle with
        | None -> $"Task '%s{name}'. Handle was not set." |> Log.debug
        | Some handle ->
            match! handle() with
            | Error error -> $"Task '%s{name}'. Failed: %s{error}" |> Log.error
            | Ok msg -> $"Task '%s{name}'. Successful. %s{msg}" |> Log.debug
    }

let rec private handleTasks name tasks (ct: CancellationToken) =
    async {
        if ct.IsCancellationRequested then
            ct.ThrowIfCancellationRequested()

        do! Infrastructure.DSL.Seq.parallelOrSequential name tasks handleTask
        
        if ct.IsCancellationRequested then
            ct.ThrowIfCancellationRequested()
        
        tasks
        |> Seq.map (fun task -> handleTasks (Some task.Name) task.Steps ct)
        |> Seq.iter Async.Start
    }

let private merge tasks handlers =
    
    let rec innerLoop (name: string option) (tasks: Core.Task list) (handlers: Core.TaskHandler list) =
        tasks
        |> List.map (fun task ->
            let name =
                let parentName = Option.defaultValue "" name

                match parentName with
                | "" -> task.Name
                | _ -> $"{parentName}.{task.Name}"

            match handlers |> List.tryFind (fun handler -> handler.Name = task.Name) with
            | None -> Error $"Handler %s{name} was not found."
            | Some handler ->

                match innerLoop (Some name) task.Steps handler.Steps with
                | Error error -> Error error
                | Ok steps ->
                    Ok
                        {   Name = task.Name
                            IsParallel = task.IsParallel
                            Handle = handler.Handle
                            Schedule = task.Schedule
                            Steps = steps })
        |> Infrastructure.DSL.Seq.resultOrError

    innerLoop None tasks handlers

let rec private startTask getSchedule =
    fun name task ->
        async {
            match! getSchedule name with
            | Error error -> $"Task '%s{name}'. Failed: %s{error}" |> Log.error
            | Ok schedule ->
                let! ct = Scheduler.getExpirationToken name schedule

                match ct.IsCancellationRequested with
                | true -> $"Task '%s{name}'. Stopped." |> Log.warning
                | false ->
                    $"Task '%s{name}'. Started." |> Log.info
                    do! handleTask name task
                    do! handleTasks (Some name) task.Steps ct
                    $"Task '%s{name}'. Completed." |> Log.debug
                    
                    match schedule with
                    | None -> $"Task '%s{name}' was run once" |> Log.warning
                    | Some schedule ->

                        $"Task '%s{name}'. Next run will be in {schedule.Delay}." |> Log.trace

                        do! Async.Sleep schedule.Delay
                        do! startTask getSchedule name task
        }

let start configure =
    async {
        Log.info "Worker started."
        match! configure() with
        | Error error -> error |> Log.error
        | Ok config ->
            match config.Tasks |> merge <| config.Handlers with
            | Error error -> error |> Log.error
            | Ok tasks ->
                let handleTask = startTask config.getSchedule
                match! Infrastructure.DSL.Seq.parallelOrSequential None tasks handleTask |> Async.Catch with
                | Choice1Of2 _ -> $"All tasks completed successfully." |> Log.info
                | Choice2Of2 ex ->
                    match ex with
                    | :? OperationCanceledException -> $"Worker was stopped." |> Log.warning
                    | _ -> $"Worker failed: %s{ex.Message}" |> Log.error
    }