module Repository

open Persistence.Scope

let getConfiguredTaskNames = SettingsStorage.getTaskNames

let getTask name =
    async { return SettingsStorage.getTask name }

module private FileStorageRepository =
    let logTaskStep stream message = FileStorage.writeLine stream message
    let getTaskSteps stream size = FileStorage.readLines stream size

let logTaskStep scope message =
    match scope with
    | FileStorageScope stream -> FileStorageRepository.logTaskStep stream message
    | InMemoryStorageScope -> async { return Error "Not implemented" }
