module Worker.Domain

open System

module Persistence =

    type Schedule() =
        member val IsEnabled: bool = false with get, set
        member val StartWork: Nullable<DateTime> = Nullable() with get, set
        member val StopWork: Nullable<DateTime> = Nullable() with get, set
        member val WorkDays: string = String.Empty with get, set
        member val Delay: string = String.Empty with get, set
        member val TimeShift: byte = 0uy with get, set

    type Task() =
        member val Name: string = String.Empty with get, set
        member val Parallel: bool = false with get, set
        member val Recurcive: bool = false with get, set
        member val Schedule: Schedule = Schedule() with get, set
        member val Steps: Task[] = [||] with get, set

module Core =
    open Infrastructure.Domain.Graph

    type Schedule =
        { StartWork: DateTime
          StopWork: DateTime option
          WorkDays: DayOfWeek Set
          Delay: TimeSpan option
          TimeShift: byte }

    type Task =
        { Name: string
          Parallel: bool
          Recurcive: bool
          Schedule: Schedule option }

        interface INodeName with
            member this.Name = this.Name

    type TaskHandler =
        { Name: string
          Handle: (Threading.CancellationTokenSource -> Async<Result<string, string>>) option }

        interface INodeName with
            member this.Name = this.Name

open Infrastructure.Domain.Graph
open Core

type Configuration =
    { Name: string
      Tasks: Node<Task> list
      Handlers: Node<TaskHandler> list
      getSchedule: string -> Async<Result<Schedule option, string>> }
