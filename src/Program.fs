open StepHandlers
open Domain.Core
open Core

[<EntryPoint>]
let main args =

    let handlers =
        Map
            [ "Task_1" |> TaskName,
              [ { Name = "Step_1" |> StepName
                  Handle = Task1.handleStep
                  Steps =
                    [ { Name = "Step_1.1" |> StepName
                        Handle = Task1.handleStep
                        Steps = [] }
                      { Name = "Step_1.2" |> StepName
                        Handle = Task1.handleStep
                        Steps = [] } ] } ]
              "Task_2" |> TaskName,
              [ { Name = "Step_1" |> StepName
                  Handle = Task2.handleStep
                  Steps = [] } ] ]

    startWorker args handlers
