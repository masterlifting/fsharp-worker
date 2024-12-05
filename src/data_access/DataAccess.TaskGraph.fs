module Worker.DataAccess.TaskGraph

open System
open Worker.DataAccess.Schedule

type TaskGraph() =
    member val Name: string = String.Empty with get, set
    member val Enabled: bool = false with get, set
    member val Recursively: string option = None with get, set
    member val Parallel: bool = false with get, set
    member val Duration: string option = None with get, set
    member val Wait: bool = false with get, set
    member val Schedule: Schedule option = None with get, set
    member val Tasks: TaskGraph[] = [||] with get, set
