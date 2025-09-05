[<AutoOpen>]
module Worker.Domain.TaskNode

open System
open Infrastructure.Domain

type TaskNode = {
    Id: Tree.NodeId
    Enabled: bool
    Recursively: TimeSpan option
    Parallel: bool
    Duration: TimeSpan
    WaitResult: bool
    Schedule: Schedule option
    Description: string option
} with

    interface Tree.INode with
        member this.Id = this.Id
        member this.set id = { this with Id = id }
