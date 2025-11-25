[<AutoOpen>]
module Worker.Domain.WorkerTask

open System
open System.Threading
open Infrastructure.Domain

type internal WorkerTaskId =
    | WorkerTaskId of Tree.NodeId

    member this.NodeId =
        match this with
        | WorkerTaskId id -> id

    member this.Value =
        match this with
        | WorkerTaskId id -> id.Value

    static member create value =
        value |> Tree.NodeId.create |> WorkerTaskId

    override this.ToString() = this.Value

type internal WorkerTaskHandler<'a> = (ActiveTask * 'a * CancellationToken -> Async<Result<unit, Error'>>) option

type internal WorkerTask<'a> = {
    Id: WorkerTaskId
    Recursively: TimeSpan option
    Parallel: bool
    Duration: TimeSpan
    WaitResult: bool
    Schedule: Schedule option
    Handler: WorkerTaskHandler<'a>
    Description: string option
} with

    member this.ToActiveTask schedule attempt = {
        Id = this.Id.Value |> ActiveTaskId
        Attempt = attempt
        Recursively = this.Recursively
        Parallel = this.Parallel
        Duration = this.Duration
        Schedule = schedule
        Description = this.Description
    }

    member this.Print(attempt: uint<attempts>) =
        match this.Description with
        | Some description -> $"%i{attempt}. '{this.Id}' %s{description}."
        | None -> $"%i{attempt}. '{this.Id}'"
