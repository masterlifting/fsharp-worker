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

module Worker =
    open Settings

    type WorkerTask =
        { Name: string
          Settings: WorkerTaskSettings }

    type WorkerTaskStepHandler = Map<string, (unit -> Async<Result<string, string>>)>
