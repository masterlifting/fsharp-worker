module Serialization

open System.Text.Json

let toJsonString data =
    try
        Ok <| JsonSerializer.Serialize data
    with ex ->
        Error ex.Message

let fromJsonString<'a> (data: string) =
    try
        Ok <| JsonSerializer.Deserialize<'a> data
    with ex ->
        Error ex.Message
