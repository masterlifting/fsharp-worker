open StepHandlers
open Domain.Core
open Core

[<EntryPoint>]
let main args =

    let handlers =
        [ { Name = "Task_1"
            Steps =
              [ { Name = "Step_1"
                  Handle = Task1.handleStep1
                  Steps = [] }
                { Name = "Step_2"
                  Handle = Task1.handleStep2
                  Steps = [] } ] }
          { Name = "Task_2"
            Steps =
              [ { Name = "Step_1"
                  Handle = Task2.handleStep1
                  Steps = [] } ] } ]


    startWorker args handlers
