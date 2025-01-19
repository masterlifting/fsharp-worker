[<AutoOpen>]
module Worker.Domain.WorkerConfiguration

open Microsoft.Extensions.Configuration
open Infrastructure.Domain

type WorkerConfiguration =
    { RootNodeId: Graph.NodeId
      RootNodeName: string
      Configuration: IConfigurationRoot
      getTaskNode: Graph.NodeId -> Async<Result<Graph.Node<WorkerTaskNode>, Error'>> }
