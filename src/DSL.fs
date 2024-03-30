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
