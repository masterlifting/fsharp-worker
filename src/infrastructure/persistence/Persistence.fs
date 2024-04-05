module Persistence

open System.IO

type Type =
    | FileStorage of string
    | InMemoryStorage

module Scope =
    type Type =
        | FileStorageScope of FileStream
        | InMemoryStorageScope of MemoryStream

    let create persistenceType =
        match persistenceType with
        | FileStorage path ->
            try
                let stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite)
                Ok <| FileStorageScope stream
            with ex ->
                Error ex.Message
        | InMemoryStorage ->
            let stream = new MemoryStream()
            Ok <| InMemoryStorageScope stream

    let remove scope =
        match scope with
        | FileStorageScope stream -> stream.Dispose()
        | InMemoryStorageScope stream -> stream.Dispose()

