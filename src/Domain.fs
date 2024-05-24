module Worker.Domain

open System

module Persistence =
    open Infrastructure.Domain.Graph

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

        interface INodeName with
            member this.Name = this.Name

module Core =
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
          Schedule: Schedule option
          Handle: NodeHandle
          Refresh: NodeRefresh<Task> }

        interface INodeHandle<Task> with
            member this.Name = this.Name
            member this.Parallel = this.Parallel
            member this.Recursively = this.Recursively
            member this.Duration = this.Duration
            member this.Handle = this.Handle
            member this.Refresh = this.Refresh

    type TaskHandler =
        { Name: string
          Handle: NodeHandle }

        interface INodeName with
            member this.Name = this.Name

open Infrastructure.Domain.Graph
open Core

type Configuration =
    { Name: string
      Tasks: Node<Task> list
      Handlers: Node<TaskHandler> list
      Refresh: NodeRefresh<Task> }
