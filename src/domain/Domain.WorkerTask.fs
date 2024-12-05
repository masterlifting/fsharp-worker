[<AutoOpen>]
module Worker.Domain.WorkerTask

open System

type WorkerTaskOut =
    { Name: string
      Recursively: TimeSpan option
      Parallel: bool
      Duration: TimeSpan
      Schedule: WorkerSchedule }
