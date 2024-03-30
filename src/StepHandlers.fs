module StepHandlers

open System
open Domain.Persistence

module AskBelgrade =

    let getAvaliableDates () = async { return Ok "" }
    let requestToKdmid () = async { return Error "What" }
    let requestToExternal () = async { return Ok "" }
    let sendAvaliableDates () = async { return Ok "" }


module Task2 =

    let private getData () =
        [| new Kdmud(None, 1, 1, 0, None, DateTime.Now) |]

    let private processData (data: Kdmud seq) =
        data |> Seq.map (fun x -> Error "Not implemented")

    let private saveData data = Ok ""

    let handleStep1 () =
        async { return getData () |> processData |> saveData }
