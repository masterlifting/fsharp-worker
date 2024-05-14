module Worker.Domain

open System
open Infrastructure.Domain

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
        interface IGraphNodeName with
            member this.Name = this.Name
            
    type TaskHandler =
        { Name: string
          Handle: (unit -> Async<Result<string, string>>) option }
        interface IGraphNodeName with
            member this.Name = this.Name
        
type Configuration =
    { Tasks: Graph<Core.Task> list
      Handlers: Graph<Core.TaskHandler> list
      getSchedule: string -> Async<Result<Core.Schedule option, string>> }
