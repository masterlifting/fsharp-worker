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
    member val Enabled: bool = false with get, set
    member val Recursively: string option = None with get, set
    member val Parallel: bool = false with get, set
    member val Duration: string option = None with get, set
    member val Wait: bool = false with get, set
    member val Schedule: ScheduleEntity option = None with get, set
    member val Description: string option = None with get, set
    member val Tasks: TaskGraphEntity[] | null = [||] with get, set

    member this.ToDomain() =
        match this.Tasks with
        | null -> List.empty |> Ok
        | tasks -> tasks |> Seq.map _.ToDomain() |> Result.choose
        |> Result.bind (fun tasks ->
            result {
                let! id = Graph.NodeId.create this.Id
                let! recursively = this.Recursively |> Option.toResult parseTimeSpan
                let! duration = this.Duration |> Option.toResult parseTimeSpan
                let! schedule = this.Schedule |> Option.toResult _.ToDomain()

                return {
                    Id = id
                    Enabled = this.Enabled
                    Recursively = recursively
                    Parallel = this.Parallel
                    Duration = duration |> Option.defaultValue (TimeSpan.FromMinutes 2.)
                    Wait = this.Wait
                    Schedule = schedule
                    Description = this.Description
                }
            }
            |> Result.map (fun task -> Graph.Node(task, tasks)))

module private Configuration =
    open Persistence.Storages.Configuration

    let private loadData = Read.section<TaskGraphEntity>
    let get client =
        client |> loadData |> Result.bind _.ToDomain() |> async.Return

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

let get storage =
    match storage |> toPersistenceStorage with
    | Storage.Configuration client -> client |> Configuration.get
    | _ -> $"The '{storage}' is not supported." |> NotSupported |> Error |> async.Return
