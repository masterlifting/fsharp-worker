[<AutoOpen>]
module Worker.Domain.WorkerTask

open System

type WorkerTask =
    { Name: string
      Recursively: TimeSpan option
      Parallel: bool
      Duration: TimeSpan
      Schedule: Schedule }
