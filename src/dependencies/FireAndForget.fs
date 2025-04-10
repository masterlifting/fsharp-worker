[<RequireQualifiedAccess>]
module internal Worker.Dependencies.FireAndForget

open System
open System.Threading
open Microsoft.Extensions.Configuration
open Infrastructure.Domain
open Worker.Domain

type Dependencies = {
    ActiveTask: ActiveTask
    Duration: TimeSpan
    Configuration: IConfigurationRoot
    startHandler: ActiveTask * IConfigurationRoot * CancellationToken -> Async<Result<unit, Error'>>
}
