module Repository

let getConfiguredTaskNames = SettingsStorage.getTaskNames

let getTask name =
    async { return SettingsStorage.getTask name }

module private FileStorageRepository =
    open System

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
                let mutable error: string option = None
                let mutable index = 0
                let mutable steps = []

                let mappedSteps = lines |> Array.ofList |> Array.map Mapper.mapStringToStep

                while error.IsNone && index < mappedSteps.Length do
                    match mappedSteps.[index] with
                    | Ok step -> steps <- step :: steps
                    | Error msg -> error <- Some msg

                return
                    match error with
                    | Some msg -> Error msg
                    | None -> Ok steps
        }

let saveTaskStep scope step =
    match scope with
    | Persistence.Scope.Type.FileStorageScope stream -> FileStorageRepository.saveTaskStep stream step
    | Persistence.Scope.Type.InMemoryStorageScope _ -> async { return Error "Not implemented" }

let getTaskSteps scope size =
    match scope with
    | Persistence.Scope.Type.FileStorageScope stream -> FileStorageRepository.getTaskSteps stream size
    | Persistence.Scope.Type.InMemoryStorageScope _ -> async { return Error "Not implemented" }
