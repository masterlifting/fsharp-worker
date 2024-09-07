module Worker.Domain

open System
open Infrastructure
open System.Threading
open Microsoft.Extensions.Configuration

type Schedule =
    { StartDate: DateOnly option
      StopDate: DateOnly option
      StartTime: TimeOnly option
      StopTime: TimeOnly option
      Workdays: DayOfWeek Set
      TimeShift: int8 }

type SchedulerStopReason =
    | NotWorkday of DayOfWeek
    | StopDateReached of DateOnly
    | StopTimeReached of TimeOnly

    member this.Message =
        match this with
        | NotWorkday day -> $"Not workday: {day}"
        | StopDateReached date -> $"Stop date reached: {date}"
        | StopTimeReached time -> $"Stop time reached: {time}"

type Scheduler =
    | Started of Schedule option
    | StartIn of TimeSpan * Schedule option
    | Stopped of SchedulerStopReason * Schedule option
    | StopIn of TimeSpan * Schedule option

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
      Duration: TimeSpan
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

type WorkerConfiguration =
    { Name: string
      Configuration: IConfigurationRoot 
      getTask: GetTask }

type HandleNodeDeps =
    { NodeName: string
      getNode: GetTask
      handleNode: uint -> Schedule option -> Task -> Async<Schedule option> }

type internal FireAndForgetDeps =
    { Configuration: IConfigurationRoot
      Duration: TimeSpan
      Schedule: Schedule option
      startHandler: TaskHandler }

module External =

    type Schedule() =
        member val StartDate: string option = None with get, set
        member val StopDate: string option = None with get, set
        member val StartTime: string option = None with get, set
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
