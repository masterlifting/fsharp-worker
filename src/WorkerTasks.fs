module WorkerTasks

open Domain.Worker

module Belgrade =
    let private checkAvailableDates () =
        async { return Ok "Belgrade - CheckAvailableDates Info" }

    let StepHandler: WorkerTaskStepHandler =
        Map [ "CheckAvailableDates", checkAvailableDates ]

module Vena =
    let checkAvailableDates () =
        async { return Ok "Vena - CheckAvailableDates Info" }

    let StepHandler: WorkerTaskStepHandler =
        Map [ "CheckAvailableDates", checkAvailableDates ]
