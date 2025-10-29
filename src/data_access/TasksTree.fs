module Worker.DataAccess.TasksTree

open System
open Infrastructure.Domain
open Infrastructure.Prelude
open Infrastructure.Prelude.Tree.Builder
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
        let rec toNode (e: TaskNodeEntity) =
            result {
                let! recursively = e.Recursively |> Option.toResult parseTimeSpan
                let! duration = e.Duration |> Option.toResult parseTimeSpan
                let! schedule = e.Schedule |> Option.toResult _.ToDomain()

                let node =
                    Tree.Node.create (
                        e.Id,
                        {
                            Enabled = e.Enabled
                            Recursively = recursively
                            Parallel = e.Parallel
                            Duration = duration |> Option.defaultValue (TimeSpan.FromMinutes 2.)
                            WaitResult = e.WaitResult
                            Schedule = schedule
                            Description = e.Description
                        }
                    )

                match e.Tasks with
                | null
                | [||] -> return node
                | tasks ->
                    let! children = tasks |> Array.map toNode |> Result.choose

                    return node |> withChildren children
            }

        this |> toNode

module private Configuration =
    open Persistence.Storages.Configuration

    let private loadData = Read.section<TaskNodeEntity>
    let get client =
        client |> loadData |> Result.bind _.ToDomain() |> async.Return

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
