[<RequireQualifiedAccess>]
module internal Worker.Dependencies.WorkerTaskNode

open Infrastructure.Domain
open Worker.Domain

type Dependencies = {
    getNode: Graph.NodeId -> Async<Result<Graph.Node<WorkerTaskNode>, Error'>>
    handleNode: uint<attempts> -> Schedule option -> WorkerTaskNode -> Async<Schedule option>
}
