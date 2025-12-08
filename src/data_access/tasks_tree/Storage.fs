[<RequireQualifiedAccess>]
module Worker.DataAccess.Storage.TasksTree

open Infrastructure.Domain
open Persistence
open Persistence.Storages
open Persistence.Storages.Domain
open Worker.DataAccess

type StorageType =
    | Configuration of Configuration.Connection
    | Postgre of Postgre.Connection

let private toProvider =
    function
    | TasksTree.Storage.Provider provider -> provider

let init storageType =
    match storageType with
    | Configuration connection -> connection |> Storage.Connection.Configuration |> Storage.init
    | Postgre connection ->
        Storage.Connection.Database {
            Database = Database.Postgre connection.String
            Lifetime = connection.Lifetime
        }
        |> Storage.init
    |> Result.map TasksTree.Storage.Provider

let dispose storage =
    storage |> toProvider |> Storage.dispose

module Query =

    let get storage =
        let provider = storage |> toProvider
        match provider with
        | Storage.Database database ->
            match database with
            | Database.Client.Postgre client -> client |> Postgre.TasksTree.Query.get
        | Storage.Configuration client -> client |> Configuration.TasksTree.Query.get
        | Storage.FileSystem _
        | Storage.InMemory _ -> $"The '{provider}' is not supported." |> NotSupported |> Error |> async.Return
