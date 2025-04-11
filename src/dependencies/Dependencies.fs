module Worker.Dependencies

open System
open System.Threading
open Microsoft.Extensions.Configuration
open Infrastructure.Domain
open Worker.Domain

[<RequireQualifiedAccess>]
module Worker =
    type Dependencies = {
        Name: string
        Configuration: IConfigurationRoot
        RootTaskId: Graph.NodeId
        tryFindTask: Graph.NodeId -> Async<Result<Graph.Node<WorkerTask> option, Error'>>
    }

[<RequireQualifiedAccess>]
module WorkerTask =
    type Dependencies = {
        tryFindTask: Graph.NodeId -> Async<Result<Graph.Node<WorkerTask> option, Error'>>
        tryStartTask: uint<attempts> -> Schedule option -> WorkerTask -> Async<Schedule option>
    }

[<RequireQualifiedAccess>]
module internal FireAndForget =
    type Dependencies = {
        ActiveTask: ActiveTask
        Duration: TimeSpan
        Configuration: IConfigurationRoot
        startActiveTask: ActiveTask * IConfigurationRoot * CancellationToken -> Async<Result<unit, Error'>>
    }
