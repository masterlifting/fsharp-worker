module Worker.DataAccess.Configuration.TasksTree

open Persistence.Storages.Configuration
open Worker.DataAccess

module Query =

    let get client =
        client
        |> Read.section<TasksTree.NodeEntity>
        |> Result.bind _.ToDomain()
        |> async.Return
