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
} with

    static member print task =
        match task.Description with
        | Some description -> $"%i{task.Attempt}.'%s{task.Id.Value}' %s{description}."
        | None -> $"%i{task.Attempt}.'%s{task.Id.Value}'"
