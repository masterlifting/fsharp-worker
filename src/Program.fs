open StepHandlers
open Domain.Core
open Core

[<EntryPoint>]
let main args =

    let handlers =
        [ { Name = "Task_1" |> TaskName
            Steps =
              [ { Name = "Step_1" |> StepName
                  Handle = Task1.handleStep
                  Steps = [] }
                { Name = "Step_2" |> StepName
                  Handle = Task1.handleStep
                  Steps = [] } ] }
          { Name = "Task_2" |> TaskName
            Steps =
              [ { Name = "Step_1" |> StepName
                  Handle = Task2.handleStep
                  Steps = [] } ] } ]


    startWorker args handlers
