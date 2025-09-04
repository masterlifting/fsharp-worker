[<AutoOpen>]
module Worker.Domain.ActiveTask

open System
open Infrastructure.Domain

type ActiveTaskId =
    | ActiveTaskId of Tree.NodeId

    member this.Value =
        match this with
        | ActiveTaskId id -> id

    member this.ValueStr = this.Value.Value

type ActiveTask = {
    Id: ActiveTaskId
    Attempt: uint<attempts>
    Recursively: TimeSpan option
    Parallel: bool
    Duration: TimeSpan
    Schedule: Schedule
    Description: string option
} with

    static member print task =
        match task.Description with
        | Some description -> $"%i{task.Attempt}.'%s{task.Id.ValueStr}' %s{description}."
        | None -> $"%i{task.Attempt}.'%s{task.Id.ValueStr}'"
