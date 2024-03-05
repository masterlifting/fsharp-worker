module StepHandlers

open Domain.Worker

[<Literal>]
let CheckAvailableDates = "CheckAvailableDates"

module Belgrade =
    let private checkAvailableDates () =
        async { return Ok "Belgrade - CheckAvailableDates Info" }

    let Handler: WorkerTaskStepHandler =
        Map [ CheckAvailableDates, checkAvailableDates ]

module Vena =
    let private checkAvailableDates () =
        async { return Ok "Vena - CheckAvailableDates Info" }

    let Handler: WorkerTaskStepHandler =
        Map [ CheckAvailableDates, checkAvailableDates ]
