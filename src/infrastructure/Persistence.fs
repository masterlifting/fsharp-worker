module Persistence
open Domain.Core
open System.IO
open System

type RepositoryType = {
    saveStep: string -> TaskStepState -> Async<unit>
    loadLastSteps: string -> Async<TaskStepState list>
}

let private saveStep taskName step =
   async {
            let path = $"{Environment.CurrentDirectory}/tasks"
            let file = $"{path}/{taskName}.json"

            if not (Directory.Exists(path)) then
                Directory.CreateDirectory(path) |> ignore

            let state =
                $"{{\"status\":\"{step.Status}\",\"attempts\":{step.Attempts},\"message\":\"{step.Message}\",\"updated_at\":\"{step.UpdatedAt}\"}};"

            do! File.AppendAllLinesAsync(file, [ state ]) |> Async.AwaitTask
        }

let private taskStepStateFromString (str: string) =
    let parts = str.Split(':')
    let id = parts.[0]
    let status = parts.[1]
    let attempts = parts.[2]
    let message = parts.[3]
    let updatedAt = parts.[4]
    
    {   Id = id
        Status = status |> Option.ofObj |> Option.defaultValue Pending
        Attempts = int attempts
        Message = message
        UpdatedAt = DateTime.Parse(updatedAt) }

let private loadLastSteps taskName =
     async {
        let path = $"{Environment.CurrentDirectory}/tasks"
        let file = $"{path}/{taskName}.json"
    
        if File.Exists file then
            let! content = File.ReadAllTextAsync file |> Async.AwaitTask
            return content.Split(';') |> Seq.map (fun x -> x |> taskStepStateFromString) |> Seq.toList
        else
            return []
}

let Repository = {
    saveStep = saveStep
    loadLastSteps = loadLastSteps
}