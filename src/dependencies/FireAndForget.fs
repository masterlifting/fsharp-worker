[<RequireQualifiedAccess>]
module internal Worker.Dependencies.FireAndForget

open System
open System.Threading
open Microsoft.Extensions.Configuration
open Infrastructure.Domain
open Worker.Domain

type Dependencies =
    { Task: WorkerTask
      Duration: TimeSpan
      Configuration: IConfigurationRoot
      startHandler: WorkerTask * IConfigurationRoot * CancellationToken -> Async<Result<WorkerTaskResult, Error'>> }
