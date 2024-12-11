[<AutoOpen>]
module Worker.Domain.WorkerConfiguration

open Microsoft.Extensions.Configuration
open Infrastructure.Domain

type WorkerConfiguration =
    { Name: string
      Configuration: IConfigurationRoot
      getTaskNode: string -> Async<Result<Graph.Node<TaskGraph>, Error'>> }
