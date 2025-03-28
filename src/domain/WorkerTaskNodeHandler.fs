[<AutoOpen>]
module Worker.Domain.WorkerTaskNodeHandler

open System.Threading
open Microsoft.Extensions.Configuration
open Infrastructure.Domain

type WorkerTaskNodeHandler = {
    Id: Graph.NodeId
    Name: string
    Handler: (WorkerTask * IConfigurationRoot * CancellationToken -> Async<Result<WorkerTaskResult, Error'>>) option
} with

    interface Graph.INode with
        member this.Id = this.Id
        member this.Name = this.Name
        member this.set(id, name) = { this with Id = id; Name = name }
