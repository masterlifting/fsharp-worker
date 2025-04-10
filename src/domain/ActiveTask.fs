[<AutoOpen>]
module Worker.Domain.ActiveTask

open System
open Infrastructure.Domain

type ActiveTask = {
    Id: Graph.NodeId
    Attempt: uint<attempts>
    Recursively: TimeSpan option
    Parallel: bool
    Duration: TimeSpan
    Schedule: Schedule
    Description: string option
}
