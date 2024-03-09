module StepHandlers

open Domain.Worker

[<Literal>]
let CheckAvailableDates = "CheckAvailableDates"

module Belgrade =
    open Domain.Persistence

    let checkAvailableDates (data: Kdmid[]) = async { return Ok data }

module Vena =
    open Domain.Persistence

    let checkAvailableDates (data: Kdmud[]) = async { return Ok data }
