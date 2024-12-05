module Worker.Domain

open System
open System.Threading
open Microsoft.Extensions.Configuration
open Infrastructure
open Domain

type WorkerTaskHandler =
    WorkerTaskOut * IConfigurationRoot * CancellationToken -> Async<Result<WorkerTaskResult, Error'>>

type WorkerHandler =
    { Name: string
      Task: WorkerTaskHandler option }

    interface Graph.INodeName with
        member this.Id = Graph.NodeId.New
        member this.Name = this.Name
        member this.set(_, name) = { this with Name = name }

type GetWorkerTask = string -> Async<Result<Graph.Node<WorkerTaskIn>, Error'>>

type WorkerConfiguration =
    { Name: string
      Configuration: IConfigurationRoot
      getTask: GetWorkerTask }

type WorkerNodeDeps =
    { getNode: GetWorkerTask
      handleNode: uint -> WorkerSchedule option -> WorkerTaskIn -> Async<WorkerSchedule option> }

type internal FireAndForgetDeps =
    { Task: WorkerTaskOut
      Duration: TimeSpan
      Configuration: IConfigurationRoot
      startHandler: WorkerTaskHandler }
