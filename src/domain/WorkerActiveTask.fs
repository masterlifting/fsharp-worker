[<AutoOpen>]
module Worker.Domain.WorkerActiveTask

open System
open Infrastructure.Domain

type WorkerActiveTask = {
    Id: Graph.NodeId
    Name: string
    Recursively: TimeSpan option
    Parallel: bool
    Duration: TimeSpan
    Schedule: Schedule
}
