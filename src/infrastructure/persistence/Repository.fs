module Repository

let getConfiguredTaskNames = SettingsStorage.getTaskNames

let getTask name =
    async { return SettingsStorage.getTask name }

module private FileStorageRepository =
    let saveTaskStep stream step =
        match Mapper.mapStepToString step with
        | Ok data -> FileStorage.writeLine stream data
        | Error error -> async { return Error error }

    let getTaskSteps stream size =
        async {
            let! linesResult = FileStorage.readLines stream size
            match linesResult with
            | Error error -> return Error error
            | Ok lines ->
                let mappedSteps = lines |> List.map Mapper.mapStringToStep
                let errorStep = mappedSteps |> List.tryFind (function | Error _ -> true | _ -> false)
                match errorStep with
                | Some (Error error) -> return Error error
                | _ -> return Ok lines
        }

let saveTaskStep scope step =
    match scope with
    | Persistence.Scope.Type.FileStorageScope stream -> FileStorageRepository.saveTaskStep stream step
    | Persistence.Scope.Type.InMemoryStorageScope _ -> async { return Error "Not implemented" }

let getTaskSteps scope size =
    match scope with
    | Persistence.Scope.Type.FileStorageScope stream -> FileStorageRepository.getTaskSteps stream size
    | Persistence.Scope.Type.InMemoryStorageScope _ -> async { return Error "Not implemented" }
