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

type TasksTreeEntity() =
    member val Id: string = String.Empty with get, set
    member val Enabled: bool = false with get, set
    member val Recursively: string option = None with get, set
    member val Parallel: bool = true with get, set
    member val Duration: string option = None with get, set
    member val WaitResult: bool = false with get, set
    member val Schedule: ScheduleEntity option = None with get, set
    member val Description: string option = None with get, set
    member val Tasks: TasksTreeEntity[] | null = [||] with get, set

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

module private Configuration =
    open Persistence.Storages.Configuration

    let private loadData = Read.section<TasksTreeEntity>
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
