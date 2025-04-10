[<RequireQualifiedAccess>]
module internal Worker.Dependencies.WorkerTask

open Infrastructure.Domain
open Worker.Domain

type Dependencies = {
    tryFindNode: Graph.NodeId -> Async<Result<Graph.Node<WorkerTask> option, Error'>>
    handleNode: uint<attempts> -> Schedule option -> WorkerTask -> Async<Schedule option>
}
