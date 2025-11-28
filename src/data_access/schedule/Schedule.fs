[<RequireQualifiedAccess>]
module Worker.DataAccess.Schedule

open System
open Infrastructure.Domain
open Infrastructure.Prelude
open Persistence
open Worker.Domain

let private result = ResultBuilder()

[<Literal>]
let private DATE_FORMAT = "yyyy-MM-dd"
[<Literal>]
let private TIME_FORMAT = "HH:mm:ss"

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
                "Workdays is not supported. Expected values: 'mon,tue,wed,thu,fri,sat,sun'."
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

let private parseDateOnly day =
    match day with
    | AP.IsDateOnly value -> Ok value
    | _ ->
        $"DateOnly is not supported. Expected format: '{DATE_FORMAT}'."
        |> NotSupported
        |> Error

let private parseTimeOnly time =
    match time with
    | AP.IsTimeOnly value -> Ok value
    | _ ->
        $"TimeOnly is not supported. Expected format: '{TIME_FORMAT}'."
        |> NotSupported
        |> Error

type Storage = Provider of Storage.Provider

type Entity() =
    member val Name: string = String.Empty with get, set
    member val StartDate: string | null = null with get, set
    member val StopDate: string | null = null with get, set
    member val StartTime: string | null = null with get, set
    member val StopTime: string | null = null with get, set
    member val Workdays: string | null = null with get, set
    member val TimeZone: Nullable<uint8> = Nullable() with get, set

    member this.ToDomain() =

        result {
            let! workdays = this.Workdays |> parseWorkdays
            let! startDate = this.StartDate |> Option.ofObj |> Option.toResult parseDateOnly
            let! stopDate = this.StopDate |> Option.ofObj |> Option.toResult parseDateOnly
            let! startTime = this.StartTime |> Option.ofObj |> Option.toResult parseTimeOnly
            let! stopTime = this.StopTime |> Option.ofObj |> Option.toResult parseTimeOnly

            return {
                Name = this.Name
                StartDate = startDate
                StopDate = stopDate
                StartTime = startTime
                StopTime = stopTime
                Workdays = workdays
                Recursively = false
                TimeZone = this.TimeZone |> Option.ofNullable |> Option.defaultValue 1uy
            }
        }

type private Schedule with
    member private this.ToEntity() =
        Entity(
            Name = this.Name,
            StartDate = (this.StartDate |> Option.map _.ToString(DATE_FORMAT) |> Option.toObj),
            StopDate = (this.StopDate |> Option.map _.ToString(DATE_FORMAT) |> Option.toObj),
            StartTime = (this.StartTime |> Option.map _.ToString(TIME_FORMAT) |> Option.toObj),
            StopTime = (this.StopTime |> Option.map _.ToString(TIME_FORMAT) |> Option.toObj),
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
