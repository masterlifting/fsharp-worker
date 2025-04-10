[<RequireQualifiedAccess>]
module internal Worker.Dependencies.FireAndForget

open System
open System.Threading
open Microsoft.Extensions.Configuration
open Infrastructure.Domain
open Worker.Domain

type Dependencies = {
    Task: WorkerActiveTask
    Duration: TimeSpan
    Configuration: IConfigurationRoot
    startHandler: WorkerActiveTask * IConfigurationRoot * CancellationToken -> Async<Result<WorkerTaskResult, Error'>>
}
