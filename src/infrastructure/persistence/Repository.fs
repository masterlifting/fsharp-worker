module Repository

open Persistence.Scope

let getConfiguredTaskNames = SettingsStorage.getTaskNames

let getTask name =
    async { return SettingsStorage.getTask name }

module private FileStorageRepository =
    let saveTaskStep stream step =
        match Mapper.TaskStep.toPersistenceString step with
        | Error error -> async { return Error error }
        | Ok data -> FileStorage.writeLine stream data

    let getTaskSteps stream size =
        async {
            let! readLines = FileStorage.readLines stream size

            return
                match readLines with
                | Error error -> Error error
                | Ok lines -> lines |> Seq.map Mapper.TaskStep.fromPersistenceString |> DSL.resultOrError
        }

let saveTaskStep scope step =
    match scope with
    | FileStorageScope stream -> FileStorageRepository.saveTaskStep stream step
    | InMemoryStorageScope _ -> async { return Error "Not implemented" }

let getTaskSteps scope size =
    match scope with
    | FileStorageScope stream -> FileStorageRepository.getTaskSteps stream size
    | InMemoryStorageScope _ -> async { return Error "Not implemented" }
