[<AutoOpen>]
module Worker.Domain.TaskGraph

open System
open Infrastructure.Domain

type TaskGraph = {
    Id: Graph.NodeId
    Enabled: bool
    Recursively: TimeSpan option
    Parallel: bool
    Duration: TimeSpan
    WaitResult: bool
    Schedule: Schedule option
    Description: string option
} with

    interface Graph.INode with
        member this.Id = this.Id
        member this.set id = { this with Id = id }
