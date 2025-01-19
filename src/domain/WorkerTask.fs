[<AutoOpen>]
module Worker.Domain.WorkerTask

open System
open Infrastructure.Domain

type WorkerTask =
    { Id: Graph.NodeId
      Name: string
      Recursively: TimeSpan option
      Parallel: bool
      Duration: TimeSpan
      Schedule: Schedule }
