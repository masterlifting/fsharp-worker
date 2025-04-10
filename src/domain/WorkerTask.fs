[<AutoOpen>]
module Worker.Domain.WorkerTask

open System
open System.Threading
open Microsoft.Extensions.Configuration
open Infrastructure.Domain
open Infrastructure.Prelude

type WorkerTask = {
    Id: Graph.NodeId
    Name: string
    Recursively: TimeSpan option
    Parallel: bool
    Duration: TimeSpan
    Wait: bool
    Schedule: Schedule option
    Handler: (WorkerActiveTask * IConfigurationRoot * CancellationToken -> Async<Result<WorkerTaskResult, Error'>>) option
} with

    interface Graph.INode with
        member this.Id = this.Id
        member this.set id = { this with Id = id }
    
    member this.ToActiveTask schedule = {
        Id = this.Id
        Name = this.Name
        Recursively = this.Recursively
        Parallel = this.Parallel
        Duration = this.Duration
        Schedule = schedule
    }
