module Persistence

open Domain.Core
open System.IO
open System
open System.Text.Json

type RepositoryType =
    { setStepState: string -> TaskStepState -> Async<unit>
      getLastStepState: string -> Async<TaskStepState option> }

let private saveStep taskName stepState =
    async {
        let path = $"{Environment.CurrentDirectory}/tasks"
        let file = $"{path}/{taskName}.json"

        if not (Directory.Exists(path)) then
            Directory.CreateDirectory(path) |> ignore

        let state: Domain.Persistence.StepState =
            { Id = stepState.Id
              Status =
                match stepState.Status with
                | Pending -> "Pending"
                | Running -> "Running"
                | Completed -> "Completed"
                | Failed -> "Failed"
              Attempts = stepState.Attempts
              Message = stepState.Message
              UpdatedAt = stepState.UpdatedAt }

        do!
            File.AppendAllLinesAsync(file, [ JsonSerializer.Serialize state ])
            |> Async.AwaitTask
    }

let private getLastStep taskName =
    async {
        let path = $"{Environment.CurrentDirectory}/tasks"
        let file = $"{path}/{taskName}.json"

        if File.Exists file then
            let! content = File.ReadAllLinesAsync file |> Async.AwaitTask
            return content |> Seq.tryLast |> Option.map (fun x -> JsonSerializer.Deserialize<Domain.Persistence.StepState>(x)) |> Option.map (fun x ->
                { Id = x.Id
                  Status =
                    match x.Status with
                    | "Pending" -> Pending
                    | "Running" -> Running
                    | "Completed" -> Completed
                    | "Failed" -> Failed
                    | _ -> Failed
                  Attempts = x.Attempts
                  Message = x.Message
                  UpdatedAt = x.UpdatedAt })
        else
            return None
    }

let Repository =
    { setStepState = saveStep
      getLastStepState = getLastStep }
