[<AutoOpen>]
module internal Worker.Domain.Dependencies

open System
open System.Threading
open Microsoft.Extensions.Configuration
open Infrastructure

type WorkerNodeDeps =
    { getNode: string -> Async<Result<Graph.Node<TaskGraph>, Error'>>
      handleNode: uint -> Schedule option -> TaskGraph -> Async<Schedule option> }

type FireAndForgetDeps =
    { Task: WorkerTask
      Duration: TimeSpan
      Configuration: IConfigurationRoot
      startHandler: WorkerTask * IConfigurationRoot * CancellationToken -> Async<Result<WorkerTaskResult, Error'>> }
