module Worker.Domain

open System
open Infrastructure.Domain.Errors

module Persistence =

    type Schedule() =
        member val IsEnabled: bool = false with get, set
        member val Delay: string = String.Empty with get, set

        member val Limit: int = 0 with get, set
        member val StartWork: Nullable<DateTime> = Nullable() with get, set
        member val StopWork: Nullable<DateTime> = Nullable() with get, set
        member val WorkDays: string = String.Empty with get, set
        member val TimeShift: byte = 0uy with get, set

    type Task() =
        member val Name: string = String.Empty with get, set
        member val Parallel: bool = false with get, set
        member val Recursively: bool = false with get, set
        member val Duration: string = String.Empty with get, set
        member val Schedule: Schedule = Schedule() with get, set
        member val Steps: Task[] = [||] with get, set

module Core =
    open System.Threading
    open Infrastructure.Domain.Graph

    type Schedule =
        { StartWork: DateTime
          StopWork: DateTime option
          WorkDays: DayOfWeek Set
          Delay: TimeSpan option
          Limit: uint option
          TimeShift: byte }

    type Task =
        { Name: string
          Parallel: bool
          Recursively: bool
          Duration: TimeSpan option
          Schedule: Schedule option }

        interface INodeName with
            member this.Name = this.Name

    type TaskHandler =
        { Name: string
          Handle: (CancellationToken -> Async<Result<string, AppError>>) option }

        interface INodeName with
            member this.Name = this.Name

open Infrastructure.Domain.Graph
open Core

type Configuration =
    { Name: string
      Tasks: Node<Task> list
      Handlers: Node<TaskHandler> list
      getSchedule: string -> Async<Result<Schedule option, string>> }
