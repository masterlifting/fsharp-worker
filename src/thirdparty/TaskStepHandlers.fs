module TaskStepHandlers

module ExternalTask =
    let step_1 () = async { return Ok "Data received" }

    module Step1 =
        let step_1_1 () = async { return Ok "Data locked" }
        let step_1_2 () = async { return Ok "Data processed" }

    let step_2 () = async { return Ok "Data sent" }
