module StepHandlers

[<Literal>]
let CheckAvailableDates = "CheckAvailableDates"

module Belgrade =
    open Domain.Persistence

    let checkAvailableDates (data: Kdmid[]) = Ok data

module Vena =
    open Domain.Persistence

    let checkAvailableDates (data: Kdmud[]) = Ok data
