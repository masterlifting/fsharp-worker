module Persistence

open System.IO

type Type =
    | FileStorage of string
    | InMemoryStorage
    | ConfigurationStorage

module Scope =
    type Type =
        | FileStorageScope of FileStream
        | InMemoryStorageScope
        | ConfigurationStorageScope

    let create persistenceType =
        match persistenceType with
        | FileStorage path ->
            try
                let stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite)
                Ok <| FileStorageScope stream
            with ex ->
                Error ex.Message
        | InMemoryStorage -> Error "In-memory storage is not supported"
        | ConfigurationStorage -> Error "Configuration storage is not supported"

    let remove scope =
        match scope with
        | FileStorageScope stream -> stream.Dispose()
        | InMemoryStorageScope -> ignore ()
        | ConfigurationStorageScope -> ignore ()
