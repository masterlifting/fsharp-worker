[<AutoOpen>]
module Worker.Domain.WorkerConfiguration

open Microsoft.Extensions.Configuration
open Infrastructure.Domain

type WorkerConfiguration = {
    Name: string
    TaskNodeRootId: Graph.NodeId
    Configuration: IConfigurationRoot
    tryFindTaskNode: Graph.NodeId -> Async<Result<Graph.Node<WorkerTaskNode> option, Error'>>
}
