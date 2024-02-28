module Helpers

open System

module Parsers =
    let (|IntParse|) (s: string) =
        match Int32.TryParse s with
        | (true, intValue) -> Some intValue
        | _ -> None
