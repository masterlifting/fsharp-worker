module Serialization
open System.Text.Json

let toJsonString data =
    try
        data |> JsonSerializer.Serialize |> Ok
    with ex ->
        Error ex.Message

let fromJsonString<'a> (data: string) =
    try
        JsonSerializer.Deserialize<'a> data |> Ok
    with ex ->
        Error ex.Message
