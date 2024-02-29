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

module Infrastructure =
    type WorkerDbContext = { ConnectionString: string }

    type Logger =
        { logInfo: string -> unit
          logWarning: string -> unit
          logError: string -> unit }

    type ServiceLocator =
        { getConfig: unit -> Microsoft.Extensions.Configuration.IConfigurationRoot
          getDbContext: unit -> WorkerDbContext
          getLogger: unit -> Logger }

open Settings

type WorkerTask =
    { Name: string
      Settings: WorkerTaskSettings }
