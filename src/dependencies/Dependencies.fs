module Worker.Dependencies

open System
open System.Threading
open Infrastructure.Domain
open Worker.Domain

[<RequireQualifiedAccess>]
module Worker =
    type Dependencies<'a> = {
        Name: string
        RootTaskId: WorkerTaskId
        Storage: Persistence.Storage.Connection
        Handlers: Tree.Node<WorkerTaskHandler<'a>>
        TaskDeps: 'a
    }

[<RequireQualifiedAccess>]
module internal WorkerTask =
    type Dependencies<'a> = {
        findTask: WorkerTaskId -> Async<Result<Tree.Node<WorkerTask<'a>> option, Error'>>
        tryStartTask: uint<attempts> -> Schedule option -> WorkerTask<'a> -> Async<Schedule option>
    }

[<RequireQualifiedAccess>]
module internal FireAndForget =
    type Dependencies<'a> = {
        ActiveTask: ActiveTask
        Duration: TimeSpan
        TaskDeps: 'a
        startActiveTask: ActiveTask * 'a * CancellationToken -> Async<Result<unit, Error'>>
    }
