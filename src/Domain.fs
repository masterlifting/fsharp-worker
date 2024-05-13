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
          TimeShift: byte}

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
          TimeShift: byte}

    type Task =
        { Name: string
          IsParallel: bool
          Schedule: Schedule option
          Steps: Task list }

    type TaskHandler =
        { Name: string
          Handle: (unit -> Async<Result<string, string>>) option
          Steps: TaskHandler list }

type Configuration =
    { Tasks: Core.Task list
      Handlers: Core.TaskHandler list
      getSchedule: string -> Async<Result<Core.Schedule option, string>> }
