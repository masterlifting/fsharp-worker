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

let private result = ResultBuilder()

let private parseTimeSpan timeSpan =
    match timeSpan with
    | AP.IsTimeSpan value -> Ok value
    | _ ->
        "Worker. TimeSpan is not supported. Expected format: 'dd.hh:mm:ss'."
        |> NotSupported
        |> Error

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

    member this.ToNode(handler: WorkerTaskNodeHandler option) =
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
                Duration = duration |> Option.defaultValue (TimeSpan.FromMinutes 2.)
                Wait = this.Wait
                Schedule = schedule
                Handler = handler |> Option.bind _.Handler
            }
        }

    member this.ToGraph() =
        match this.Tasks with
        | null -> List.empty |> Ok
        | tasks -> tasks |> Seq.map _.ToGraph() |> Result.choose
        |> Result.bind (fun tasks -> this.ToNode None |> Result.map (fun task -> Graph.Node(task, tasks)))

module private Configuration =
    open Persistence.Storages.Configuration

    let private loadData = Read.section<TaskGraphEntity>
    let getSimple client =
        client |> loadData |> Result.bind _.ToGraph() |> async.Return

    let getWithHandlers handlers client =

        let rec mergeLoop (taskId: Graph.NodeId option) (graph: TaskGraphEntity) =
            Graph.NodeId.create graph.Id
            |> Result.map (fun nodeId -> [ taskId; Some nodeId ] |> List.choose id |> Graph.Node.Id.combine)
            |> Result.bind (fun nodeId ->
                let handler =
                    match graph.Enabled with
                    | false -> None
                    | true ->
                        handlers
                        |> Graph.BFS.tryFindById nodeId
                        |> Option.map _.Value
                
                graph.ToNode handler
                |> Result.bind (fun node ->
                    match graph.Tasks with
                    | null -> Graph.Node(node, []) |> Ok
                    | tasks ->
                        tasks
                        |> Array.map (mergeLoop (Some nodeId))
                        |> Result.choose
                        |> Result.map (fun children -> Graph.Node(node, children))))

        let startMerge graph = graph |> mergeLoop None

        client |> loadData |> Result.bind startMerge |> async.Return

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

let getSimple storage =
    match storage |> toPersistenceStorage with
    | Storage.Configuration client -> client |> Configuration.getSimple
    | _ -> $"The '{storage}' is not supported." |> NotSupported |> Error |> async.Return

let getWithHandlers handlers storage =
    match storage |> toPersistenceStorage with
    | Storage.Configuration client -> client |> Configuration.getWithHandlers handlers
    | _ -> $"The '{storage}' is not supported." |> NotSupported |> Error |> async.Return
