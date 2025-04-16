[<AutoOpen>]
module Worker.Domain.WorkerTask

open System
open System.Threading
open Microsoft.Extensions.Configuration
open Infrastructure.Domain
open Infrastructure.Prelude

type WorkerTask = {
    Id: Graph.NodeId
    Recursively: TimeSpan option
    Parallel: bool
    Duration: TimeSpan
    WaitResult: bool
    Schedule: Schedule option
    Handler: (ActiveTask * IConfigurationRoot * CancellationToken -> Async<Result<unit, Error'>>) option
    Description: string option
} with

    interface Graph.INode with
        member this.Id = this.Id
        member this.set id = { this with Id = id }

    member this.ToActiveTask schedule attempt = {
        Id = this.Id |> ActiveTaskId
        Attempt = attempt
        Recursively = this.Recursively
        Parallel = this.Parallel
        Duration = this.Duration
        Schedule = schedule
        Description = this.Description
    }

    member this.Print(attempt: uint<attempts>) =
        match this.Description with
        | Some description -> $"%i{attempt}.'%s{this.Id.Value}' %s{description}."
        | None -> $"%i{attempt}.'%s{this.Id.Value}'"
