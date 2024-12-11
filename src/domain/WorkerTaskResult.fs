[<AutoOpen>]
module Worker.Domain.WorkerTaskResult

type WorkerTaskResult =
    | Success of obj
    | Warn of string
    | Debug of string
    | Info of string
    | Trace of string
