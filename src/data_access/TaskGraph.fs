module Worker.DataAccess.TaskGraph

open System
open Infrastructure.Domain
open Infrastructure.Prelude
open Persistence
open Worker.Domain
open Worker.DataAccess.Schedule

type TaskGraphStorage = TaskGraphStorage of Storage.Type
type StorageType = Configuration of Configuration.Domain.Client

type TaskGraphEntity() =
    member val Name: string = String.Empty with get, set
    member val Enabled: bool = false with get, set
    member val Recursively: string option = None with get, set
    member val Parallel: bool = false with get, set
    member val Duration: string option = None with get, set
    member val Wait: bool = false with get, set
    member val Schedule: ScheduleEntity option = None with get, set
    member val Tasks: TaskGraphEntity[] = [||] with get, set

    member this.ToDomain (handler: WorkerTaskNodeHandler) enabled =
        let result = ResultBuilder()

        let inline parseTimeSpan timeSpan =
            match timeSpan with
            | AP.IsTimeSpan value -> Ok value
            | _ -> "TimeSpan. Expected format: 'dd.hh:mm:ss'." |> NotSupported |> Error

        result {
            let! recursively = this.Recursively |> Option.toResult parseTimeSpan
            let! duration = this.Duration |> Option.toResult parseTimeSpan
            let! schedule = this.Schedule |> Option.toResult _.ToDomain()

            return
                { Name = this.Name
                  Parallel = this.Parallel
                  Recursively = recursively
                  Duration = duration |> Option.defaultValue (TimeSpan.FromMinutes 5.)
                  Wait = this.Wait
                  Schedule = schedule
                  Handler =
                    match enabled with
                    | true -> handler.Handler
                    | false -> None }
        }

module private Configuration =
    open Persistence.Configuration

    let private loadData = Query.get<TaskGraphEntity>

    let private merge (handlers: Graph.Node<WorkerTaskNodeHandler>) taskGraph =

        let rec mergeLoop parentTaskName (graph: TaskGraphEntity) =
            let fullTaskName = parentTaskName |> Graph.buildNodeName graph.Name

            match handlers |> Graph.BFS.tryFindByName fullTaskName with
            | None -> $"%s{fullTaskName} handler" |> NotFound |> Error
            | Some handler ->
                graph.ToDomain handler.Value graph.Enabled
                |> Result.bind (fun workerTask ->
                    match graph.Tasks with
                    | null -> Graph.Node(workerTask, []) |> Ok
                    | tasks ->
                        tasks
                        |> Array.map (mergeLoop (Some fullTaskName))
                        |> Result.choose
                        |> Result.map (fun children -> Graph.Node(workerTask, children)))

        taskGraph |> mergeLoop None

    let create section handlers client =
        client |> loadData section |> Result.bind (merge handlers) |> async.Return

let private toPersistenceStorage storage =
    storage
    |> function
        | TaskGraphStorage storage -> storage

let init storageType =
    match storageType with
    | Configuration connection ->
        connection
        |> Storage.Connection.Configuration
        |> Storage.init
        |> Result.map TaskGraphStorage

let create handlers storage =
    match storage |> toPersistenceStorage with
    | Storage.Configuration client -> client.Configuration |> Configuration.create client.SectionName handlers
    | _ -> $"Storage {storage}" |> NotSupported |> Error |> async.Return
