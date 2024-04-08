open System
open Domain.Core
open TaskStepHandlers

[<EntryPoint>]
let main args =

    let handlers =
        [ { Name = "ExternalTask"
            Steps =
              [ { Name = "Step_1"
                  Handle = ExternalTask.step_1
                  Steps =
                    [ { Name = "Step_1_1"
                        Handle = ExternalTask.Step1.step_1_1
                        Steps = [] }
                      { Name = "Step_1_2"
                        Handle = ExternalTask.Step1.step_1_2
                        Steps = [] } ] }
                { Name = "Step_2"
                  Handle = ExternalTask.step_2
                  Steps = [] } ] } ]

    let duration =
        match args.Length with
        | 1 ->
            match args.[0] with
            | DSL.AP.IsFloat seconds -> seconds
            | _ -> (TimeSpan.FromDays 1).TotalSeconds
        | _ -> (TimeSpan.FromDays 1).TotalSeconds

    Core.startWorker duration handlers

    0
