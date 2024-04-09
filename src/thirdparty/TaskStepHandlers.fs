module TaskStepHandlers

open Domain.Core

module ExternalTask =
    let step_1 () = async { return Ok "Data received" }

    module Step1 =
        let step_1_1 () = async { return Ok "Data locked" }
        let step_1_2 () = async { return Ok "Data processed" }

    let step_2 () = async { return Ok "Data sent" }

let taskHandlers =
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
              Steps = [] }
            { Name = "Step_3"
              Handle = ExternalTask.step_2
              Steps =
                [ { Name = "Step_3_1"
                    Handle = ExternalTask.step_1
                    Steps = [] }
                  { Name = "Step_3_2"
                    Handle = ExternalTask.step_2
                    Steps = [] } ] } ] } ]
