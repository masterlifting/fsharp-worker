module StepHandlers

open System
open Domain.Persistence

module Task1 =

    let private getData () =
        [| new Kdmid(None, 1, 1, 0, None, DateTime.Now) |]
        
    let private processData (data: Kdmid seq) = data |> Seq.map (fun x -> Ok x)
        
    let private saveData data = Error "Not implemented"
        
    let handleStep taskName stepName = async { return getData () |> processData |> saveData }
module Task2 =

    let private getData () =
        [| new Kdmud(None, 1, 1, 0, None, DateTime.Now) |]

    let private processData (data: Kdmud seq) =
        data |> Seq.map (fun x -> Error "Not implemented")

    let private saveData data = Ok ""

    let handleStep taskName stepName = async { return getData () |> processData |> saveData }
