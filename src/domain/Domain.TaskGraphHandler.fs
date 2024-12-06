[<AutoOpen>]
module Worker.Domain.TaskGraphHandler

open System.Threading
open Microsoft.Extensions.Configuration
open Infrastructure

type TaskGraphHandler =
    { Name: string
      Handler: (WorkerTask * IConfigurationRoot * CancellationToken -> Async<Result<WorkerTaskResult, Error'>>) option }

    interface Graph.INodeName with
        member this.Id = Graph.NodeId.New
        member this.Name = this.Name
        member this.set(_, name) = { this with Name = name }
