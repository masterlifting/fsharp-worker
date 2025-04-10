[<AutoOpen>]
module Worker.Domain.WorkerTaskHandler

open System.Threading
open Microsoft.Extensions.Configuration
open Infrastructure.Domain

type WorkerTaskHandler = {
    Id: Graph.NodeId
    Handler: (WorkerActiveTask * IConfigurationRoot * CancellationToken -> Async<Result<WorkerTaskResult, Error'>>) option
} with

    interface Graph.INode with
        member this.Id = this.Id
        member this.set id = { this with Id = id }
