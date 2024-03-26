open StepHandlers
open Domain.Core
open Core

[<EntryPoint>]
let main args =

    let handlers =
        Map
            [ "Task_1",
              [ { Name = "Step_1"
                  Handler = fun () -> async { return Task1.getData () |> Task1.processData |> Task1.saveData }
                  Steps =
                    [ { Name = "Step_1.1"
                        Handler = fun () -> async { return Task1.getData () |> Task1.processData |> Task1.saveData }
                        Steps = [] }
                      { Name = "Step_1.2"
                        Handler = fun () -> async { return Task1.getData () |> Task1.processData |> Task1.saveData }
                        Steps = [] } ] } ]
              "Task_2",
              [ { Name = "Step_1"
                  Handler = fun () -> async { return Task2.getData () |> Task2.processData |> Task2.saveData }
                  Steps = [] } ] ]

    startWorker args handlers
