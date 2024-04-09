module Domain

open System

module Settings =
    open System.Collections.Generic

    [<CLIMutable>]
    type TaskShchedulerSettings =
        { IsEnabled: bool
          IsOnce: bool
          StartWork: Nullable<DateTime>
          StopWork: Nullable<DateTime>
          WorkDays: string
          Delay: string
          TimeShift: byte }

    [<CLIMutable>]
    type TaskStepSettings =
        { Name: string
          IsParallel: bool
          Steps: TaskStepSettings[] }

    [<CLIMutable>]
    type TaskSettings =
        { Steps: TaskStepSettings[]
          Scheduler: TaskShchedulerSettings }

    [<CLIMutable>]
    type Section =
        { Tasks: Dictionary<string, TaskSettings> }

module Core =
    type TaskSchedulerSettings =
        { IsEnabled: bool
          IsOnce: bool
          TimeShift: byte
          StartWork: DateTime
          StopWork: DateTime option
          WorkDays: DayOfWeek Set
          Delay: TimeSpan }

    type TaskStepSettings =
        { Name: string
          IsParallel: bool
          Steps: TaskStepSettings list }

    type TaskSettings =
        { Name: string
          Steps: TaskStepSettings list
          Scheduler: TaskSchedulerSettings }

    type TaskStepHandler =
        { Name: string
          Handle: unit -> Async<Result<string, string>>
          Steps: TaskStepHandler list }

    type TaskHandler =
        { Name: string
          Steps: TaskStepHandler list }

    type TaskStep =
        { Name: string
          IsParallel: bool
          Handle: unit -> Async<Result<string, string>>
          Steps: TaskStep list }

    type WorkerConfiguration =
        { Duration: float
          Tasks: TaskSettings seq
          Handlers: TaskHandler seq
          getTask: string -> Async<Result<TaskSettings, string>> }
