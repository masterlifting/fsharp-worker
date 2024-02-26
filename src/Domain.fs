module Domain

open System

    module Configurations =
        type TaskShchedule = {
            IsEnable: bool
            IsOnce: bool
            TimeShift: byte
            StartWork: Nullable<DateTime>
            StopWork: Nullable<DateTime>
            StartTime: Nullable<TimeOnly>
            StopTime: Nullable<TimeOnly>
            WorkTime: string
            WorkDays: string
        }
        type TAskItem = {
            ChunkSize: int
            IsInfinite: bool
            Steps: string
            Schedule: TaskShchedule
        }
        type WorkerSettings = {
            Tasks: Map<string, TAskItem>
        }