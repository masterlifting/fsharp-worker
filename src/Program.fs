open StepHandlers
open Domain.Core
open Core

[<EntryPoint>]
let main args =

    let handlers =
        [ { Name = "AskBelgrade"
            Steps =
              [ { Name = "GetAvaliableDates"
                  Handle = AskBelgrade.getAvaliableDates
                  Steps =
                    [ { Name = "RequestToKdmid"
                        Handle = AskBelgrade.requestToKdmid
                        Steps = [] }
                      { Name = "RequestToExternal"
                        Handle = AskBelgrade.requestToExternal
                        Steps = [] } ] }
                { Name = "SendAvaliableDates"
                  Handle = AskBelgrade.sendAvaliableDates
                  Steps = [] } ] }
          { Name = "ParseReports"
            Steps =
              [ { Name = "Step_1"
                  Handle = Task2.handleStep1
                  Steps = [] } ] } ]

    let duration =
        match args.Length with
        | 1 ->
            match args.[0] with
            | DSL.IsFloat seconds -> seconds
            | _ -> (System.TimeSpan.FromDays 1).TotalSeconds
        | _ -> (System.TimeSpan.FromDays 1).TotalSeconds

    startWorker duration handlers

    0
