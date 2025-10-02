[<AutoOpen>]
module Worker.Domain.TaskNode

open System

type TaskNode = {
    Enabled: bool
    Recursively: TimeSpan option
    Parallel: bool
    Duration: TimeSpan
    WaitResult: bool
    Schedule: Schedule option
    Description: string option
}
