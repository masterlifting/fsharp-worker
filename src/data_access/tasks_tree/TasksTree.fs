[<RequireQualifiedAccess>]
module Worker.DataAccess.TasksTree

open System
open Infrastructure.Domain
open Infrastructure.Prelude
open Infrastructure.Prelude.Tree.Builder
open Persistence
open Worker.Domain

type Storage = Provider of Storage.Provider

let private result = ResultBuilder()

let private parseTimeSpan timeSpan =
    match timeSpan with
    | AP.IsTimeSpan value -> Ok value
    | _ ->
        "Worker. TimeSpan is not supported. Expected format: 'dd.hh:mm:ss'."
        |> NotSupported
        |> Error

type NodeEntity() =
    member val Id: string = String.Empty with get, set
    member val Enabled: bool = false with get, set
    member val Parallel: bool = true with get, set
    member val Duration: string | null = null with get, set
    member val WaitResult: bool = false with get, set
    member val Description: string | null = null with get, set
    member val Schedule: Schedule.Entity | null = null with get, set
    member val Tasks: NodeEntity[] | null = null with get, set

    member this.ToDomain() =
        let toOption =
            function
            | AP.IsString s -> Some s
            | _ -> None

        let rec toNode (e: NodeEntity) =
            result {
                let! duration = e.Duration |> toOption |> Option.toResult parseTimeSpan
                let! schedule = e.Schedule |> Option.ofObj |> Option.toResult _.ToDomain()

                let node =
                    Tree.Node.create (
                        e.Id,
                        {
                            Enabled = e.Enabled
                            Parallel = e.Parallel
                            Duration = duration |> Option.defaultValue (TimeSpan.FromMinutes 2.)
                            WaitResult = e.WaitResult
                            Schedule = schedule
                            Description = e.Description |> Option.ofObj
                        }
                    )

                match e.Tasks with
                | null
                | [||] -> return node
                | tasks ->
                    let! children = tasks |> Array.map toNode |> Result.choose

                    return node |> withChildren children
            }

        this |> toNode
