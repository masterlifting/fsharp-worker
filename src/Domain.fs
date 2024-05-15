module Worker.Domain

open System

module Persistence =

    [<CLIMutable>]
    type Schedule =
        { IsEnabled: bool
          IsOnce: bool
          StartWork: Nullable<DateTime>
          StopWork: Nullable<DateTime>
          WorkDays: string
          Delay: string
          TimeShift: byte }

    [<CLIMutable>]
    type Task =
        { Name: string
          IsParallel: bool
          Schedule: Schedule option
          Steps: Task[] }

module Core =
    open Infrastructure.Domain.Graph

    type Schedule =
        { IsEnabled: bool
          IsOnce: bool
          StartWork: DateTime
          StopWork: DateTime option
          WorkDays: DayOfWeek Set
          Delay: TimeSpan
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
