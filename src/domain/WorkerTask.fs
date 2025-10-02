[<AutoOpen>]
module Worker.Domain.WorkerTask

open System
open System.Threading
open Microsoft.Extensions.Configuration
open Infrastructure.Domain

type WorkerTaskHandler = (ActiveTask * IConfigurationRoot * CancellationToken -> Async<Result<unit, Error'>>) option

type WorkerTask = {
    Id: string
    Recursively: TimeSpan option
    Parallel: bool
    Duration: TimeSpan
    WaitResult: bool
    Schedule: Schedule option
    Handler: WorkerTaskHandler
    Description: string option
} with

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
        | Some description -> $"%i{attempt}.'%s{this.Id}' %s{description}."
        | None -> $"%i{attempt}.'%s{this.Id}'"
