open System
open Domain.Core

[<EntryPoint>]
let main args =

    let handlers =
        [ { Name = "AskBelgrade"
            Steps =
              [ { Name = "GetAvaliableDates"
                  Handle = StepHandlers.AskBelgrade.getAvaliableDates
                  Steps =
                    [ { Name = "GetAvaliableDates.RequestToKdmid"
                        Handle = StepHandlers.AskBelgrade.requestToKdmid
                        Steps = [] }
                      { Name = "GetAvaliableDates.RequestToExternal"
                        Handle = StepHandlers.AskBelgrade.requestToExternal
                        Steps = [] } ] }
                { Name = "SendAvaliableDates"
                  Handle = StepHandlers.AskBelgrade.sendAvaliableDates
                  Steps = [] } ] }
          { Name = "ParseReports"
            Steps =
              [ { Name = "Step_1"
                  Handle = StepHandlers.Task2.handleStep1
                  Steps = [] } ] } ]

    let duration =
        match args.Length with
        | 1 ->
            match args.[0] with
            | DSL.IsFloat seconds -> seconds
            | _ -> (TimeSpan.FromDays 1).TotalSeconds
        | _ -> (TimeSpan.FromDays 1).TotalSeconds

    Core.startWorker duration handlers

    0
