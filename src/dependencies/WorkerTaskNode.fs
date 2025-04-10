[<RequireQualifiedAccess>]
module internal Worker.Dependencies.WorkerTaskNode

open Infrastructure.Domain
open Worker.Domain

type Dependencies = {
    tryFindNode: Graph.NodeId -> Async<Result<Graph.Node<WorkerTaskNode> option, Error'>>
    handleNode: uint<attempts> -> Schedule option -> WorkerTaskNode -> Async<Schedule option>
}
