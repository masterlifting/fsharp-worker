module Domain

module Settings =
    open System
    open System.Collections.Generic

    [<CLIMutable>]
    type WorkerTaskShcheduleSettings =
        { IsEnabled: bool
          IsOnce: bool
          StartWork: Nullable<DateTime>
          StopWork: Nullable<DateTime>
          WorkDays: string
          Delay: string
          TimeShift: byte }

    [<CLIMutable>]
    type WorkerTaskSettings =
        { ChunkSize: int
          IsInfinite: bool
          Steps: string
          Schedule: WorkerTaskShcheduleSettings }

    [<CLIMutable>]
    type WorkerSettings =
        { Tasks: Dictionary<string, WorkerTaskSettings> }

module Persistence =
    open System

    type IWorkerData =
        abstract CorellationId: Guid option
        abstract StatusId: int
        abstract StepId: int
        abstract Attempts: int
        abstract Error: string option
        abstract UpdatedAt: DateTime

    type Kdmid(corellationId, statusId, stepId, attempts, error, updatedAt) =
        interface IWorkerData with
            member _.CorellationId = corellationId
            member _.StatusId = statusId
            member _.StepId = stepId
            member _.Attempts = attempts
            member _.Error = error
            member _.UpdatedAt = updatedAt

    type Kdmud(corellationId, statusId, stepId, attempts, error, updatedAt) =
        interface IWorkerData with
            member _.CorellationId = corellationId
            member _.StatusId = statusId
            member _.StepId = stepId
            member _.Attempts = attempts
            member _.Error = error
            member _.UpdatedAt = updatedAt

open Persistence

module Worker =
    open Settings

    type Step = Step of string

    type DataTypes =
        | Kdmid of Kdmid
        | Kdmud of Kdmud

    type Task =
        { Name: string
          Settings: WorkerTaskSettings }
