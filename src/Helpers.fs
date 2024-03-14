module Helpers

open System

let (|IsInt|_|) (input: string) =
    match Int32.TryParse input with
    | true, value -> Some value
    | _ -> None

let (|IsTimeSpan|_|) (input: string) =
    match TimeSpan.TryParse input with
    | true, value -> Some value
    | _ -> None

let (|HasValue|_|) (input: Nullable<'T>) =
    if input.HasValue then Some input.Value else None
