[<AutoOpen>]
module Worker.Domain.TaskGraph

open System
open Infrastructure.Domain

type TaskGraph = {
    Id: Graph.NodeId
    Name: string
    Enabled: bool
    Recursively: TimeSpan option
    Parallel: bool
    Duration: TimeSpan
    Wait: bool
    Schedule: Schedule option
} with

    interface Graph.INode with
        member this.Id = this.Id
        member this.set id = { this with Id = id }
