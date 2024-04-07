module DSL

open System

let (|IsInt|_|) (input: string) =
    match Int32.TryParse input with
    | true, value -> Some value
    | _ -> None

let (|IsFloat|_|) (input: string) =
    match Double.TryParse input with
    | true, value -> Some value
    | _ -> None

let (|IsTimeSpan|_|) (input: string) =
    match TimeSpan.TryParse input with
    | true, value -> Some value
    | _ -> None

let resultOrError collection =
    let checkItemResult state itemResult =
        match state with
        | Error error -> Error error
        | Ok items ->
            match itemResult with
            | Error error -> Error error
            | Ok item -> Ok <| item :: items

    match Seq.fold checkItemResult (Ok []) collection with
    | Error error -> Error error
    | Ok items -> Ok <| List.rev items
