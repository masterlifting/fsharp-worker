module Worker.Domain

open System

module Persistence =

    type Schedule ()=
      member val IsEnabled: bool = false with get, set
      member val IsOnce: bool = false with get, set
      member val StartWork: Nullable<DateTime> = Nullable() with get, set
      member val StopWork: Nullable<DateTime> = Nullable() with get, set
      member val WorkDays: string = "" with get, set
      member val Delay: string = "" with get, set
      member val TimeShift: byte = 0uy with get, set
    
    type Task () =
      member val Name: string = "" with get, set
      member val IsParallel: bool = false with get, set
      member val Schedule: Schedule = Schedule() with get, set
      member val Steps: Task[] = [||] with get, set
    
module Core =
    open Infrastructure.Domain.Graph

    type Schedule =
        { StartWork: DateTime
          StopWork: DateTime option
          WorkDays: DayOfWeek Set
          Delay: TimeSpan
          IsOnce: bool
          TimeShift: byte }

    type Task =
        { Name: string
          IsParallel: bool
          Schedule: Schedule option}
        interface INodeName with
            member this.Name = this.Name
            
    type TaskHandler =
        { Name: string
          Handle: (unit -> Async<Result<string, string>>) option }
        interface INodeName with
            member this.Name = this.Name
        
open Infrastructure.Domain.Graph
open Core

type Configuration =
    { Tasks: Node<Task> list
      Handlers: Node<TaskHandler> list
      getSchedule: string -> Async<Result<Schedule option, string>> }
