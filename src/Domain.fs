module Domain

module Settings =
    open System
    open System.Collections.Generic

    [<CLIMutable>]
    type WorkerTaskShcheduleSettings =
        { IsEnable: bool
          IsOnce: bool
          TimeShift: byte
          StartWork: Nullable<DateTime>
          StopWork: Nullable<DateTime>
          WorkTime: string
          WorkDays: string }

    [<CLIMutable>]
    type WorkerTaskSettings =
        { ChunkSize: int
          IsInfinite: bool
          Steps: string
          Schedule: WorkerTaskShcheduleSettings }

    [<CLIMutable>]
    type WorkerSettings =
        { Tasks: Dictionary<string, WorkerTaskSettings> }

module Worker =
    open Settings

    type WorkerTask =
        { Name: string
          Settings: WorkerTaskSettings }

    type WorkerTaskStepHandler = Map<string, Map<string, unit -> Async<Result<string, string>>>>
