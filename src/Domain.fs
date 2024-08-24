module Worker.Domain

open System
open Infrastructure
open System.Threading
open Microsoft.Extensions.Configuration

type Schedule =
    { StartWork: DateTime
      StopWork: DateTime option
      Workdays: DayOfWeek Set
      TimeShift: int8}

type TaskResult =
    | Success of Object
    | Warn of string
    | Debug of string
    | Info of string
    | Trace of string

type TaskHandler = IConfigurationRoot * Schedule option * CancellationToken -> Async<Result<TaskResult, Error'>>

type Task =
    { Name: string
      Parallel: bool
      Recursively: TimeSpan option
      Duration: TimeSpan option
      Limit: uint option 
      Schedule: Schedule option
      Await: bool
      Handler: TaskHandler option }

    interface Graph.INodeName with
        member this.Name = this.Name

type TaskNode =
    { Name: string
      Task: TaskHandler option }

    interface Graph.INodeName with
        member this.Name = this.Name

type GetTask = string -> Async<Result<Graph.Node<Task>, Error'>>

type WorkerDeps =
    { getTask: GetTask
      Configuration: IConfigurationRoot }

type HandleNodeDeps =
    { NodeName: string
      getNode: GetTask
      handleNode: uint -> CancellationToken -> Task -> Async<CancellationToken> }

type internal FireAndForgetDeps =
    { Configuration: IConfigurationRoot
      Duration: TimeSpan option
      Schedule: Schedule option
      taskHandler: TaskHandler }

module External =

    type Schedule() =
        member val StartWork: DateTime option = None with get, set
        member val StopWork: DateTime option = None with get, set
        member val Workdays: string = String.Empty with get, set
        member val TimeShift: int8 = 0y with get, set

    type TaskEnabled() =
        member val Await: bool = true with get, set

    type TaskGraph() =
        member val Name: string = String.Empty with get, set
        member val Enabled: TaskEnabled option = None with get, set
        member val Parallel: bool = false with get, set
        member val Recursively: string option  = None with get, set
        member val Duration: string option = None with get, set
        member val Limit: int = 0 with get, set
        member val Schedule: Schedule option = None with get, set
        member val Tasks: TaskGraph[] = [||] with get, set

        interface Graph.INodeName with
            member this.Name = this.Name
