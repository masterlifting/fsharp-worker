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

module Persistence =
    type TaskStepState =
        abstract CorellationId: Guid option
        abstract StatusId: int
        abstract StepId: int
        abstract Attempts: int
        abstract Error: string option
        abstract UpdatedAt: DateTime

    type Kdmid(corellationId, statusId, stepId, attempts, error, updatedAt) =
        interface TaskStepState with
            member _.CorellationId = corellationId
            member _.StatusId = statusId
            member _.StepId = stepId
            member _.Attempts = attempts
            member _.Error = error
            member _.UpdatedAt = updatedAt

    type Kdmud(corellationId, statusId, stepId, attempts, error, updatedAt) =
        interface TaskStepState with
            member _.CorellationId = corellationId
            member _.StatusId = statusId
            member _.StepId = stepId
            member _.Attempts = attempts
            member _.Error = error
            member _.UpdatedAt = updatedAt

module Core =
    type TaskScheduler =
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

    type Task =
        { Name: string
          Steps: TaskStepSettings list
          Scheduler: TaskScheduler }

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
          Tasks: Task seq
          Handlers: TaskHandler seq
          getTask: string -> Async<Result<Task, string>> }
