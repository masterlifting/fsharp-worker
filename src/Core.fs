module Worker.Core

open System
open Domain
open Domain.Core
open Infrastructure
open Infrastructure.Logging
open Infrastructure.Domain.Graph
open Infrastructure.DSL.Threading
open Worker
open System.Threading

let private merge tasks handlers =

    let rec innerLoop nodeName (tasks: Node<Task> list) (handlers: Node<TaskHandler> list) =
        tasks
        |> List.map (fun task ->
            let fillNodeName = nodeName |> DSL.Graph.buildNodeName <| task.Value.Name

            match handlers |> List.tryFind (fun handler -> handler.Value.Name = task.Value.Name) with
            | None -> Error $"Task %s{fillNodeName}. Failed: Handler was not found."
            | Some handler ->

                match innerLoop (Some fillNodeName) task.Children handler.Children with
                | Error error -> Error error
                | Ok steps ->

                    if handler.Value.Handle.IsNone then
                        $"Task '%s{fillNodeName}'. Handling function was not set." |> Log.warning

                    Ok
                    <| Node(
                        { new INodeHandle with
                            member _.Name = fillNodeName
                            member _.Parallel = task.Value.Parallel
                            member _.Recursively = task.Value.Recursively
                            member _.Duration = task.Value.Duration
                            member _.Handle = handler.Value.Handle },
                        steps
                    ))
        |> DSL.Seq.resultOrError

    innerLoop None tasks handlers

let rec private runTask getSchedule =
    fun (task: INodeHandle) parentToken count ->
        async {
            let taskName = $"Task '%s{task.Name}'."

            if parentToken |> canceled then
                $"{taskName} Canceled by parent." |> Log.warning
                return parentToken
            else
                use cts = new CancellationTokenSource()

                match! getSchedule task.Name with
                | Error error ->
                    cts.Cancel()
                    $"{taskName} Failed: %s{error}" |> Log.error
                    return cts.Token
                | Ok schedule ->

                    let! taskToken = Scheduler.getExpirationToken task schedule count cts

                    use linkedCts =
                        CancellationTokenSource.CreateLinkedTokenSource(parentToken, taskToken)

                    match linkedCts.IsCancellationRequested with
                    | true ->
                        $"{taskName} Canceled." |> Log.warning
                        return linkedCts.Token
                    | false ->

                        match task.Handle with
                        | None -> ()
                        | Some handle ->
                            $"{taskName} Started." |> Log.trace

                            use cts = new CancellationTokenSource()

                            if task.Duration.IsSome then
                                cts.CancelAfter task.Duration.Value

                            match! handle cts.Token with
                            | Error error -> $"{taskName} Failed: %s{error.Message}" |> Log.error
                            | Ok msg -> $"{taskName} Success. %s{msg}" |> Log.success

                        let completed = $"{taskName} Completed."

                        match schedule with
                        | None -> completed |> Log.debug
                        | Some schedule ->
                            match schedule.Delay with
                            | None -> completed |> Log.debug
                            | Some delay ->
                                $"%s{completed} Next task will be run in {delay}." |> Log.debug
                                do! Async.Sleep delay

                        return linkedCts.Token
        }

let start configure =
    async {
        try
            match! configure () with
            | Error error -> error |> Log.error
            | Ok config ->
                match config.Tasks |> merge <| config.Handlers with
                | Error error -> error |> Log.error
                | Ok tasks ->
                    let handleTask = runTask config.getSchedule

                    match! DSL.Graph.handleNodes tasks handleTask CancellationToken.None |> Async.Catch with
                    | Choice1Of2 _ ->
                        $"All tasks of the worker '%s{config.Name}' are started successfully."
                        |> Log.success
                    | Choice2Of2 ex ->
                        match ex with
                        | :? OperationCanceledException ->
                            let message = $"Worker '%s{config.Name}' was stopped."
                            failwith message
                        | _ -> failwith $"Worker '%s{config.Name}' failed: %s{ex.Message}"
        with ex ->
            ex.Message |> Log.error
    }
