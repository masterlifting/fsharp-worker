module Persistence

open System.IO

type Type =
    | FileStorage of string
    | InMemoryStorage

module Scope =
    type Type =
        | FileStorageScope of FileStream
        | InMemoryStorageScope

    let create persistenceType =
        match persistenceType with
        | FileStorage path ->
            try
                let stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite)
                Ok <| FileStorageScope stream
            with ex ->
                Error ex.Message
        | InMemoryStorage -> Ok InMemoryStorageScope

    let remove scope =
        match scope with
        | FileStorageScope stream -> stream.Dispose()
        | InMemoryStorageScope -> ignore ()
