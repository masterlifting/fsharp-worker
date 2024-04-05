module FileStorage

open System.IO
open System.Text
open System

let writeLine (stream: FileStream) data =
    async {
        try
            let buffer = Encoding.UTF8.GetBytes(data + Environment.NewLine)
            stream.Position <- stream.Length
            do! stream.WriteAsync(buffer, 0, buffer.Length) |> Async.AwaitTask
            do! stream.FlushAsync() |> Async.AwaitTask
            return Ok()
        with ex ->
            return Error ex.Message
    }

let readLines (stream: FileStream) number =
    async {
        try
            let sr = new StreamReader(stream)
            let mutable lines = []
            let mutable i = 0

            while i < number do
                let! line = sr.ReadLineAsync() |> Async.AwaitTask
                
                match String.IsNullOrWhiteSpace line with
                | false ->
                    lines <- line :: lines
                    i <- i + 1
                | true -> i <- number
            
            return Ok lines
            
        with ex ->
            return Error ex.Message
    }
