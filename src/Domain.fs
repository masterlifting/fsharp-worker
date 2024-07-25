module Worker.Domain

open System
open Infrastructure
open Infrastructure.Domain.Graph

module Internal =
    open System.Threading
    open Microsoft.Extensions.Configuration

    type Schedule =
        { StartWork: DateTime
          StopWork: DateTime option
          Workdays: DayOfWeek Set
          Delay: TimeSpan option
          Limit: uint option
          TimeShift: byte }

    type TaskResult =
        | Success of Object
        | Warn of string
        | Debug of string
        | Info of string
        | Trace of string

    type HandleTask = IConfigurationRoot -> CancellationToken -> Async<Result<TaskResult, Error'>>

    type Task =
        { Name: string
          Parallel: bool
          Recursively: bool
          Duration: TimeSpan option
          Schedule: Schedule option
          Handle: HandleTask option}

        interface INodeName with
            member this.Name = this.Name

    type TaskHandler =
        { Name: string
          Handle: HandleTask option}

        interface INodeName with
            member this.Name = this.Name

    type GetTask =
        string -> Async<Result<Node<Task>, Error'>>

    type WorkerDeps =
        { getTask: GetTask
          Configuration: IConfigurationRoot }
    
    type HandleNodeDeps =
        { TaskName: string
          getTask: GetTask
          handleTask: uint -> CancellationToken -> Task -> Async<CancellationToken>}

    type internal FireAndForgetDeps =
        { Configuration: IConfigurationRoot
          Duration: TimeSpan option
          handleTask: HandleTask }

module External =

    type Schedule() =
        member val IsEnabled: bool = false with get, set
        member val Delay: string = String.Empty with get, set
        member val Limit: int = 0 with get, set
        member val StartWork: Nullable<DateTime> = Nullable() with get, set
        member val StopWork: Nullable<DateTime> = Nullable() with get, set
        member val Workdays: string = String.Empty with get, set
        member val TimeShift: byte = 0uy with get, set

    type Task() =
        member val Name: string = String.Empty with get, set
        member val Parallel: bool = false with get, set
        member val Recursively: bool = false with get, set
        member val Duration: string = String.Empty with get, set
        member val Schedule: Schedule = Schedule() with get, set
        member val Steps: Task[] = [||] with get, set

        interface INodeName with
            member this.Name = this.Name
