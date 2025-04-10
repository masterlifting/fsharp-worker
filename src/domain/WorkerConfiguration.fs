[<AutoOpen>]
module Worker.Domain.WorkerConfiguration

open Microsoft.Extensions.Configuration
open Infrastructure.Domain

type WorkerConfiguration = {
    Name: string
    TaskNodeRootId: Graph.NodeId
    Configuration: IConfigurationRoot
    tryFindTask: Graph.NodeId -> Async<Result<Graph.Node<WorkerTask> option, Error'>>
}
