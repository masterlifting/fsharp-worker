module Worker.Domain

open System
open Infrastructure.Domain.Graph
open Infrastructure.Domain.Errors

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

    type HandleTask = (IConfigurationRoot -> CancellationToken -> Async<Result<TaskResult, ErrorType>>) option

    type Task =
        { Name: string
          Parallel: bool
          Recursively: bool
          Duration: TimeSpan option
          Schedule: Schedule option
          Handle: HandleTask }

        interface INodeName with
            member this.Name = this.Name

    type TaskHandler =
        { Name: string
          Handle: HandleTask }

        interface INodeName with
            member this.Name = this.Name

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
