module StepHandlers

open System
open Domain.Persistence

module Task1 =

    let getData () =
        [| new Kdmid(None, 1, 1, 0, None, DateTime.Now) |]

    let processData (data: Kdmid seq) = data |> Seq.map (fun x -> Ok x)

    let saveData data = Error "Not implemented"

module Task2 =

    let getData () =
        [| new Kdmud(None, 1, 1, 0, None, DateTime.Now) |]

    let processData (data: Kdmud seq) =
        data |> Seq.map (fun x -> Error "Not implemented")

    let saveData data = Ok ""
