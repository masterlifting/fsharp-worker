module Worker.Domain

open System
open System.Threading
open Microsoft.Extensions.Configuration
open Infrastructure

type WorkerSchedule =
    { StartDate: DateOnly option
      StopDate: DateOnly option
      StartTime: TimeOnly option
      StopTime: TimeOnly option
      Workdays: DayOfWeek Set
      TimeZone: int8 }

type WorkerSchedulerStopReason =
    | NotWorkday of DayOfWeek
    | StopDateReached of DateOnly
    | StopTimeReached of TimeOnly

    member this.Message =
        match this with
        | NotWorkday day -> $"Not workday: {day}"
        | StopDateReached date -> $"Stop date reached: {date}"
        | StopTimeReached time -> $"Stop time reached: {time}"

type WorkerScheduler =
    | NotScheduled
    | Started of WorkerSchedule
    | StartIn of TimeSpan * WorkerSchedule
    | Stopped of WorkerSchedulerStopReason * WorkerSchedule
    | StopIn of TimeSpan * WorkerSchedule

type WorkerTaskResult =
    | Success of obj
    | Warn of string
    | Debug of string
    | Info of string
    | Trace of string

type WorkerTaskHandler =
    IConfigurationRoot * WorkerSchedule * CancellationToken -> Async<Result<WorkerTaskResult, Error'>>

type WorkerTask =
    { Name: string
      Recursively: TimeSpan option
      Parallel: bool
      Duration: TimeSpan
      Wait: bool
      Schedule: WorkerSchedule option
      Handler:
          (IConfigurationRoot * WorkerSchedule * CancellationToken -> Async<Result<WorkerTaskResult, Error'>>) option }

    interface Graph.INodeName with
        member this.Name = this.Name
        member this.setName name = { this with Name = name }

type WorkerTaskNode =
    { Name: string
      Task: WorkerTaskHandler option }

    interface Graph.INodeName with
        member this.Name = this.Name
        member this.setName name = { this with Name = name }

type GetWorkerTask = string -> Async<Result<Graph.Node<WorkerTask>, Error'>>

type WorkerConfiguration =
    { Name: string
      Configuration: IConfigurationRoot
      getTask: GetWorkerTask }

type WorkerNodeDeps =
    { NodeName: string
      getNode: GetWorkerTask
      handleNode: uint -> WorkerSchedule option -> WorkerTask -> Async<WorkerSchedule option> }

type internal FireAndForgetDeps =
    { Configuration: IConfigurationRoot
      Schedule: WorkerSchedule
      Duration: TimeSpan
      startHandler: IConfigurationRoot * WorkerSchedule * CancellationToken -> Async<Result<WorkerTaskResult, Error'>> }

module External =

    type Schedule() =
        member val StartDate: string option = None with get, set
        member val StopDate: string option = None with get, set
        member val StartTime: string option = None with get, set
        member val StopTime: string option = None with get, set
        member val Workdays: string = String.Empty with get, set
        member val TimeZone: int8 = 0y with get, set

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

            member this.setName name =
                this.Name <- name
                this
