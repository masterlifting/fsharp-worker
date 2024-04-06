module Repository

open Persistence.Scope

let getConfiguredTaskNames = SettingsStorage.getTaskNames

let getTask name =
    async { return SettingsStorage.getTask name }

module private FileStorageRepository =
    let saveTaskStep stream step =
        match Mapper.toPersistenceTaskStepJsonString step with
        | Ok data -> FileStorage.writeLine stream data
        | Error error -> async { return Error error }

    let getTaskSteps stream size =
        async {
            match! FileStorage.readLines stream size with
            | Error error -> return Error error
            | Ok lines ->

                let checkMappedStep state stepResult =
                    match state with
                    | Error error -> Error error
                    | Ok steps ->
                        match stepResult with
                        | Ok step -> Ok <| step :: steps
                        | Error error -> Error error

                let checkMappedSteps () =
                    let mappedSteps = lines |> Seq.map Mapper.toCoreTaskStepFromJsonString
                    Seq.fold checkMappedStep (Ok []) mappedSteps

                return
                    match checkMappedSteps () with
                    | Error error -> Error error
                    | Ok steps -> steps |> List.rev |> Ok
        }

let saveTaskStep scope step =
    match scope with
    | FileStorageScope stream -> FileStorageRepository.saveTaskStep stream step
    | InMemoryStorageScope _ -> async { return Error "Not implemented" }

let getTaskSteps scope size =
    match scope with
    | FileStorageScope stream -> FileStorageRepository.getTaskSteps stream size
    | InMemoryStorageScope _ -> async { return Error "Not implemented" }
