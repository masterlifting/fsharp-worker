[<RequireQualifiedAccess>]
module Worker.DataAccess.Schedule

open System
open Infrastructure.Domain
open Infrastructure.Prelude
open Persistence
open Worker.Domain

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
                "Worker. Workdays is not supported. Expected values: 'mon,tue,wed,thu,fri,sat,sun'."
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
        "Worker. DateOnly is not supported. Expected format: 'yyyy-MM-dd'."
        |> NotSupported
        |> Error

let private parseTimeOnly time =
    match time with
    | AP.IsTimeOnly value -> Ok value
    | _ ->
        "Worker. TimeOnly is not supported. Expected format: 'hh:mm:ss'."
        |> NotSupported
        |> Error

type Storage = Provider of Storage.Provider

type Entity() =
    member val Id: int64 = 0L with get, set
    member val StartDate: string | null = null with get, set
    member val StopDate: string | null = null with get, set
    member val StartTime: string | null = null with get, set
    member val StopTime: string | null = null with get, set
    member val Workdays: string | null = null with get, set
    member val TimeZone: Nullable<uint8> = Nullable() with get, set

    member this.ToDomain() =
        let result = ResultBuilder()

        result {
            let! workdays = this.Workdays |> parseWorkdays
            let! startDate = this.StartDate |> Option.ofObj |> Option.toResult parseDateOnly
            let! stopDate = this.StopDate |> Option.ofObj |> Option.toResult parseDateOnly
            let! startTime = this.StartTime |> Option.ofObj |> Option.toResult parseTimeOnly
            let! stopTime = this.StopTime |> Option.ofObj |> Option.toResult parseTimeOnly

            return {
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
            Id = 0L,
            StartDate = (this.StartDate |> Option.map _.ToString("yyyy-MM-dd") |> Option.toObj),
            StopDate = (this.StopDate |> Option.map _.ToString("yyyy-MM-dd") |> Option.toObj),
            StartTime = (this.StartTime |> Option.map _.ToString("HH:mm:ss") |> Option.toObj),
            StopTime = (this.StopTime |> Option.map _.ToString("HH:mm:ss") |> Option.toObj),
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
