module StepHandlers

[<Literal>]
let CheckAvailableDates = "CheckAvailableDates"

module Belgrade =
    open Domain.Persistence
    open System

    let getData () =
        [| new Kdmid(None, 1, 1, 0, None, DateTime.Now) :> IWorkerData |]

    let processData (data: Kdmid seq) = data |> Seq.map (fun x -> Ok x)
    let saveData (data: Kdmid seq) = Error "Not implemented"

module Vena =
    open Domain.Persistence
    open System

    let getData () =
        [| new Kdmud(None, 1, 1, 0, None, DateTime.Now) :> IWorkerData |]

    let checkAvailableDates (data: Kdmud seq) = data |> Seq.map (fun x -> Ok x)
    let saveData (data: Kdmud seq) = Error "Not implemented"
