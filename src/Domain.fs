module Domain

open System

    module Configurations =
        open System.Collections.Generic
        [<CLIMutable>]
        type WorkerTaskShchedule = {
            IsEnable: bool
            IsOnce: bool
            TimeShift: byte
            StartWork: Nullable<DateTime>
            StopWork: Nullable<DateTime>
            WorkTime: string
            WorkDays: string
        }
        [<CLIMutable>]
        type WorkerTask = {
            ChunkSize: int
            IsInfinite: bool
            Steps: string
            Schedule: WorkerTaskShchedule
        }
        [<CLIMutable>]
        type WorkerSettings = {
            Tasks: Dictionary<string, WorkerTask>
        }