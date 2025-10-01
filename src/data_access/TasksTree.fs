module Worker.DataAccess.TasksTree

open System
open Infrastructure.Domain
open Infrastructure.Prelude
open Persistence
open Persistence.Storages
open Persistence.Storages.Domain
open Worker.Domain
open Worker.DataAccess.Schedule

type TasksTreeStorage = TasksTreeStorage of Storage.Provider
type StorageType = Configuration of Configuration.Connection

let private result = ResultBuilder()

let private parseTimeSpan timeSpan =
    match timeSpan with
    | AP.IsTimeSpan value -> Ok value
    | _ ->
        "Worker. TimeSpan is not supported. Expected format: 'dd.hh:mm:ss'."
        |> NotSupported
        |> Error

type TaskNodeEntity() =
    member val Id: string = String.Empty with get, set
    member val Enabled: bool = false with get, set
    member val Recursively: string option = None with get, set
    member val Parallel: bool = true with get, set
    member val Duration: string option = None with get, set
    member val WaitResult: bool = false with get, set
    member val Schedule: ScheduleEntity option = None with get, set
    member val Description: string option = None with get, set
    member val Tasks: TaskNodeEntity[] | null = [||] with get, set

    member this.ToDomain() =
        match this.Tasks with
        | null -> List.empty |> Ok
        | tasks -> tasks |> Seq.map _.ToDomain() |> Result.choose
        |> Result.bind (fun tasks ->
            result {
                let! id = Tree.NodeId.parse this.Id
                let! recursively = this.Recursively |> Option.toResult parseTimeSpan
                let! duration = this.Duration |> Option.toResult parseTimeSpan
                let! schedule = this.Schedule |> Option.toResult _.ToDomain()

                return {
                    Id = id
                    Enabled = this.Enabled
                    Recursively = recursively
                    Parallel = this.Parallel
                    Duration = duration |> Option.defaultValue (TimeSpan.FromMinutes 2.)
                    WaitResult = this.WaitResult
                    Schedule = schedule
                    Description = this.Description
                }
            }
            |> Result.map (fun task -> Tree.Node(task, tasks)))

    /// New method that returns Infrastructure.Prelude.Tree.Root instead of Infrastructure.Domain.Tree.Node
    member this.ToPreludeTreeRoot() =
        let rec convertToPreludeNode (entity: TaskNodeEntity) =
            result {
                let! id = entity.Id |> String.toDefault |> Ok // Using string directly for Prelude.Tree.Node
                let! recursively = entity.Recursively |> Option.toResult parseTimeSpan
                let! duration = entity.Duration |> Option.toResult parseTimeSpan
                let! schedule = entity.Schedule |> Option.toResult _.ToDomain()

                let taskGraph = {
                    Id = Tree.NodeId.parse entity.Id |> Result.defaultValue (Tree.NodeIdValue entity.Id)
                    Enabled = entity.Enabled
                    Recursively = recursively
                    Parallel = entity.Parallel
                    Duration = duration |> Option.defaultValue (TimeSpan.FromMinutes 2.)
                    WaitResult = entity.WaitResult
                    Schedule = schedule
                    Description = entity.Description
                }

                let preludeNode = Infrastructure.Prelude.Tree.Node.Create(entity.Id, taskGraph)
                
                // Recursively convert children
                match entity.Tasks with
                | null -> return preludeNode
                | tasks -> 
                    let! childNodes = 
                        tasks 
                        |> Array.toList
                        |> List.map convertToPreludeNode 
                        |> Result.choose
                    
                    return preludeNode.WithChildren(childNodes)
            }
        
        this |> convertToPreludeNode |> Result.map Infrastructure.Prelude.Tree.Root.Init

module private Configuration =
    open Persistence.Storages.Configuration

    let private loadData = Read.section<TaskNodeEntity>
    let get client =
        client |> loadData |> Result.bind _.ToDomain() |> async.Return
        
    let getPreludeTreeRoot client =
        client |> loadData |> Result.bind _.ToPreludeTreeRoot() |> async.Return

let private toPersistenceStorage storage =
    storage
    |> function
        | TasksTreeStorage storage -> storage

let init storageType =
    match storageType with
    | Configuration connection ->
        connection
        |> Storage.Connection.Configuration
        |> Storage.init
        |> Result.map TasksTreeStorage

let get storage =
    match storage |> toPersistenceStorage with
    | Storage.Configuration client -> client |> Configuration.get
    | _ -> $"The '{storage}' is not supported." |> NotSupported |> Error |> async.Return

/// Get tasks tree as Infrastructure.Prelude.Tree.Root instead of Infrastructure.Domain.Tree.Node
let getPreludeTreeRoot storage =
    match storage |> toPersistenceStorage with
    | Storage.Configuration client -> client |> Configuration.getPreludeTreeRoot
    | _ -> $"The '{storage}' is not supported." |> NotSupported |> Error |> async.Return
