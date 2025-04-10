[<AutoOpen>]
module Worker.Domain.WorkerTaskHandler

open System.Threading
open Microsoft.Extensions.Configuration
open Infrastructure.Domain

type WorkerTaskHandler = {
    Id: Graph.NodeId
    Handler: (ActiveTask * IConfigurationRoot * CancellationToken -> Async<Result<unit, Error'>>) option
} with

    interface Graph.INode with
        member this.Id = this.Id
        member this.set id = { this with Id = id }
