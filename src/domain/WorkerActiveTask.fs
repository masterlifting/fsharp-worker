[<AutoOpen>]
module Worker.Domain.WorkerActiveTask

open System
open Infrastructure.Domain

type WorkerActiveTask = {
    Id: Graph.NodeId
    Recursively: TimeSpan option
    Parallel: bool
    Duration: TimeSpan
    Schedule: Schedule
    Description: string option
}
