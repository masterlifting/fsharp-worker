module Worker.Domain

open System
open Infrastructure
open System.Threading
open Microsoft.Extensions.Configuration

type Schedule =
    { StartDate: DateOnly
      StopDate: DateOnly option
      StartTime: TimeOnly
      StopTime: TimeOnly option
      Workdays: DayOfWeek Set
      TimeShift: int8 }

type SchedulerStopReason =
    | NotWorkday
    | StopDateReached
    | StopTimeReached

    member this.Message =
        match this with
        | NotWorkday -> "Not workday"
        | StopDateReached -> "Stop date reached"
        | StopTimeReached -> "Stop time reached"

type Scheduler =
    | Ready of Schedule option
    | ReadyAfter of TimeSpan * Schedule option
    | Expired of SchedulerStopReason * Schedule option
    | ExpiredAfter of DateTime * Schedule option

type TaskResult =
    | Success of Object
    | Warn of string
    | Debug of string
    | Info of string
    | Trace of string

type TaskHandler = IConfigurationRoot * Schedule option * CancellationToken -> Async<Result<TaskResult, Error'>>

type Task =
    { Name: string
      Recursively: TimeSpan option
      Parallel: bool
      Duration: TimeSpan option
      Wait: bool
      Schedule: Schedule option
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
      handleNode: uint -> Schedule option -> Task -> Async<Schedule option> }

type internal FireAndForgetDeps =
    { Configuration: IConfigurationRoot
      Duration: TimeSpan option
      Schedule: Schedule option
      taskHandler: TaskHandler }

module External =

    type Schedule() =
        member val StartDate: string = String.Empty with get, set
        member val StopDate: string option = None with get, set
        member val StartTime: string = String.Empty with get, set
        member val StopTime: string option = None with get, set
        member val Workdays: string = String.Empty with get, set
        member val TimeShift: int8 = 0y with get, set

    type TaskGraph() =
        member val Name: string = String.Empty with get, set
        member val Enabled: bool = false with get, set
        member val Recursively: string option = None with get, set
        member val Parallel: bool = false with get, set
        member val Duration: string option = None with get, set
        member val Wait: bool = false with get, set
        member val Schedule: Schedule option = None with get, set
        member val Tasks: TaskGraph[] = [||] with get, set

        interface Graph.INodeName with
            member this.Name = this.Name
