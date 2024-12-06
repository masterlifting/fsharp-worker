[<AutoOpen>]
module Worker.Domain.WorkerConfiguration

open Microsoft.Extensions.Configuration
open Infrastructure

type WorkerConfiguration =
    { Name: string
      Configuration: IConfigurationRoot
      getTaskNode: string -> Async<Result<Graph.Node<TaskGraph>, Error'>> }
