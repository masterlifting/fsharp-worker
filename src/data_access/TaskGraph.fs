module Worker.DataAccess.TaskGraph

open System
open Infrastructure.Domain
open Infrastructure.Prelude
open Persistence
open Persistence.Storages
open Persistence.Storages.Domain
open Worker.Domain
open Worker.DataAccess.Schedule

type TaskGraphStorage = TaskGraphStorage of Storage.Provider
type StorageType = Configuration of Configuration.Connection

type TaskGraphEntity() =
    member val Id: string = String.Empty with get, set
    member val Name: string = String.Empty with get, set
    member val Enabled: bool = false with get, set
    member val Recursively: string option = None with get, set
    member val Parallel: bool = false with get, set
    member val Duration: string option = None with get, set
    member val Wait: bool = false with get, set
    member val Schedule: ScheduleEntity option = None with get, set
    member val Tasks: TaskGraphEntity[] | null = [||] with get, set

    member this.ToDomain (handler: WorkerTaskNodeHandler) enabled =
        let result = ResultBuilder()

        let inline parseTimeSpan timeSpan =
            match timeSpan with
            | AP.IsTimeSpan value -> Ok value
            | _ ->
                "Worker. TimeSpan is not supported. Expected format: 'dd.hh:mm:ss'."
                |> NotSupported
                |> Error

        result {
            let! id = Graph.NodeId.create this.Id
            let! recursively = this.Recursively |> Option.toResult parseTimeSpan
            let! duration = this.Duration |> Option.toResult parseTimeSpan
            let! schedule = this.Schedule |> Option.toResult _.ToDomain()

            return {
                Id = id
                Name = this.Name
                Parallel = this.Parallel
                Recursively = recursively
                Duration = duration |> Option.defaultValue (TimeSpan.FromMinutes 5.)
                Wait = this.Wait
                Schedule = schedule
                Handler =
                    match enabled with
                    | true -> handler.Handler
                    | false -> None
            }
        }

module private Configuration =
    open Persistence.Storages.Configuration

    let private loadData = Read.section<TaskGraphEntity>

    let private merge (handlers: Graph.Node<WorkerTaskNodeHandler>) taskGraph =

        let rec mergeLoop (parentTaskId: Graph.NodeId option) (graph: TaskGraphEntity) =
            Graph.NodeId.create graph.Id
            |> Result.map (fun graphId -> [ parentTaskId; Some graphId ] |> List.choose id |> Graph.Node.Id.combine)
            |> Result.bind (fun taskId ->
                match handlers |> Graph.BFS.tryFindById taskId with
                | None -> $"Task handler Id '%s{taskId.Value}'" |> NotFound |> Error
                | Some handler ->
                    graph.ToDomain handler.Value graph.Enabled
                    |> Result.bind (fun workerTask ->
                        match graph.Tasks with
                        | null -> Graph.Node(workerTask, []) |> Ok
                        | tasks ->
                            tasks
                            |> Array.map (mergeLoop (Some taskId))
                            |> Result.choose
                            |> Result.map (fun children -> Graph.Node(workerTask, children))))

        taskGraph |> mergeLoop None

    let create handlers client =
        client |> loadData |> Result.bind (merge handlers) |> async.Return

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
    | Storage.Configuration client -> client |> Configuration.create handlers
    | _ -> $"The '{storage}' is not supported." |> NotSupported |> Error |> async.Return
