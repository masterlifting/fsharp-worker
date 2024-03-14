module Helpers

open System
open Domain.Settings
open System.Collections.Generic

let (|IsInt|_|) (input: string) =
    match Int32.TryParse input with
    | true, value -> Some value
    | _ -> None

let (|IsTimeSpan|_|) (input: string) =
    match TimeSpan.TryParse input with
    | true, value -> Some value
    | _ -> None

let (|HasValue|_|) (input: Nullable<'T>) =
    if input.HasValue then Some input.Value else None

let bfsSteps (steps: WorkerTaskStepSettings[]) handle =
    let queue = Queue<WorkerTaskStepSettings>(steps)

    while queue.Count > 0 do
        let step = queue.Dequeue()
        handle step

        match step.Steps with
        | [||] -> ()
        | _ -> step.Steps |> Seq.iter queue.Enqueue

let rec dfsSteps (steps: WorkerTaskStepSettings[]) handle =
    match steps with
    | [||] -> ()
    | _ ->
        let step = steps.[0]
        handle step

        match step.Steps with
        | [||] -> ()
        | _ -> dfsSteps step.Steps handle

        dfsSteps steps.[1..] handle
