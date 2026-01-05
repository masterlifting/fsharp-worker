[<RequireQualifiedAccess>]
module Worker.DataAccess.Schedule

open System
open Infrastructure.Domain
open Infrastructure.Prelude
open Persistence
open Worker.Domain

let private result = ResultBuilder()

let private parseWorkdays workdays =
    match workdays with
    | AP.IsString str ->
        str.Split ','
        |> Array.map (function
            | "mon" -> Ok DayOfWeek.Monday
            | "tue" -> Ok DayOfWeek.Tuesday
            | "wed" -> Ok DayOfWeek.Wednesday
            | "thu" -> Ok DayOfWeek.Thursday
            | "fri" -> Ok DayOfWeek.Friday
            | "sat" -> Ok DayOfWeek.Saturday
            | "sun" -> Ok DayOfWeek.Sunday
            | _ ->
                "Workdays expected values: 'mon,tue,wed,thu,fri,sat,sun'."
                |> NotSupported
                |> Error)
        |> Result.choose
        |> Result.map Set.ofList
    | _ ->
        Ok
        <| set [
            DayOfWeek.Monday
            DayOfWeek.Tuesday
            DayOfWeek.Wednesday
            DayOfWeek.Thursday
            DayOfWeek.Friday
            DayOfWeek.Saturday
            DayOfWeek.Sunday
        ]

type Storage = Provider of Storage.Provider

type Entity() =
    member val Name: string | null = null with get, set
    member val Workdays: string | null = null with get, set
    member val Recursively: Nullable<TimeSpan> = Nullable() with get, set
    member val StartDate: Nullable<DateOnly> = Nullable() with get, set
    member val StopDate: Nullable<DateOnly> = Nullable() with get, set
    member val StartTime: Nullable<TimeOnly> = Nullable() with get, set
    member val StopTime: Nullable<TimeOnly> = Nullable() with get, set
    member val TimeZone: Nullable<uint8> = Nullable() with get, set

    member this.ToDomain() =

        result {
            let! name =
                match this.Name with
                | AP.IsString v -> Ok v
                | _ -> "Schedule name is required." |> NotSupported |> Error

            let! workdays = this.Workdays |> parseWorkdays

            return {
                Name = name
                Workdays = workdays
                Recursively = this.Recursively |> Option.ofNullable
                StartDate = this.StartDate |> Option.ofNullable
                StopDate = this.StopDate |> Option.ofNullable
                StartTime = this.StartTime |> Option.ofNullable
                StopTime = this.StopTime |> Option.ofNullable
                TimeZone = this.TimeZone |> Option.ofNullable |> Option.defaultValue 1uy
            }
        }

type internal Schedule with
    member private this.ToEntity() =
        Entity(
            Name = this.Name,
            Recursively = (this.Recursively |> Option.toNullable),
            StartDate = (this.StartDate |> Option.toNullable),
            StopDate = (this.StopDate |> Option.toNullable),
            StartTime = (this.StartTime |> Option.toNullable),
            StopTime = (this.StopTime |> Option.toNullable),
            Workdays =
                (this.Workdays
                 |> Set.map (function
                     | DayOfWeek.Monday -> "mon"
                     | DayOfWeek.Tuesday -> "tue"
                     | DayOfWeek.Wednesday -> "wed"
                     | DayOfWeek.Thursday -> "thu"
                     | DayOfWeek.Friday -> "fri"
                     | DayOfWeek.Saturday -> "sat"
                     | DayOfWeek.Sunday -> "sun"
                     | _ -> "")
                 |> String.concat ","
                 |> Some
                 |> Option.toObj),
            TimeZone = this.TimeZone
        )

let toEntity (schedule: Schedule) = schedule.ToEntity() |> Ok
