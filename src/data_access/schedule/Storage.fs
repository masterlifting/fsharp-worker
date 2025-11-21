[<RequireQualifiedAccess>]
module Worker.DataAccess.Storage.Schedule

open Infrastructure.Domain
open Persistence
open Persistence.Storages
open Persistence.Storages.Domain
open Worker.DataAccess
open Worker.Domain

type StorageType = Postgre of Postgre.Connection

let private toProvider =
    function
    | Schedule.Storage.Provider provider -> provider

let init storageType =
    match storageType with
    | Postgre connection ->
        Storage.Connection.Database {
            Database = Database.Postgre connection.String
            Lifetime = connection.Lifetime
        }
        |> Storage.init
    |> Result.map Schedule.Storage.Provider

module Query =

    let tryFindById (id: int64) storage =
        let provider = storage |> toProvider
        match provider with
        | Storage.Database database ->
            match database with
            | Database.Client.Postgre client -> client |> Postgre.Schedule.Query.tryFindById id
        | Storage.FileSystem _
        | Storage.InMemory _
        | Storage.Configuration _ -> $"The '{provider}' is not supported." |> NotSupported |> Error |> async.Return

module Command =

    let create (schedule: Schedule) storage =
        let provider = storage |> toProvider
        match provider with
        | Storage.Database database ->
            match database with
            | Database.Client.Postgre client ->
                async {
                    match Schedule.toEntity schedule with
                    | Error err -> return Error err
                    | Ok entity -> return! client |> Postgre.Schedule.Command.create entity
                }
        | Storage.FileSystem _
        | Storage.InMemory _
        | Storage.Configuration _ -> $"The '{provider}' is not supported." |> NotSupported |> Error |> async.Return

    let update (id: int64) (schedule: Schedule) storage =
        let provider = storage |> toProvider
        match provider with
        | Storage.Database database ->
            match database with
            | Database.Client.Postgre client ->
                async {
                    match Schedule.toEntity schedule with
                    | Error err -> return Error err
                    | Ok entity ->
                        let updatedEntity = entity
                        updatedEntity.Id <- id
                        return! client |> Postgre.Schedule.Command.update updatedEntity
                }
        | Storage.FileSystem _
        | Storage.InMemory _
        | Storage.Configuration _ -> $"The '{provider}' is not supported." |> NotSupported |> Error |> async.Return
