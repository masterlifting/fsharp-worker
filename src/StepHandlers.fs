module StepHandlers

open System
open Domain.Worker

[<Literal>]
let CheckAvailableDates = "CheckAvailableDates"

module Belgrade =

    let getData () =
        [| new Persistence.Kdmid(None, 1, 1, 0, None, DateTime.Now) |]

    let processData (data: Persistence.Kdmid seq) = data |> Seq.map (fun x -> Ok x)

    let saveData data = Error "Not implemented"

module Vena =

    let getData () =
        [| new Persistence.Kdmud(None, 1, 1, 0, None, DateTime.Now) |]

    let processData (data: Persistence.Kdmud seq) =
        data |> Seq.map (fun x -> Error "Not implemented")

    let saveData data = Ok ""
