module Worker.DataAccess.Schedule

open System
open Infrastructure
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
                "Workday. Expected values: 'mon,tue,wed,thu,fri,sat,sun'."
                |> NotSupported
                |> Error)
        |> Result.choose
        |> Result.map Set.ofList
    | _ ->
        Ok
        <| set
            [ DayOfWeek.Monday
              DayOfWeek.Tuesday
              DayOfWeek.Wednesday
              DayOfWeek.Thursday
              DayOfWeek.Friday
              DayOfWeek.Saturday
              DayOfWeek.Sunday ]

let private parseDateOnly day =
    match day with
    | AP.IsDateOnly value -> Ok value
    | _ -> "DateOnly. Expected format: 'yyyy-MM-dd'." |> NotSupported |> Error

let private parseTimeOnly time =
    match time with
    | AP.IsTimeOnly value -> Ok value
    | _ -> "TimeOnly. Expected format: 'hh:mm:ss'." |> NotSupported |> Error

type internal Schedule() =
    member val StartDate: string option = None with get, set
    member val StopDate: string option = None with get, set
    member val StartTime: string option = None with get, set
    member val StopTime: string option = None with get, set
    member val Workdays: string = String.Empty with get, set
    member val TimeZone: int8 = 0y with get, set

    member this.ToDomain() =
        let result = ResultBuilder()

        result {
            let! workdays = this.Workdays |> parseWorkdays
            let! startDate = this.StartDate |> Option.toResult parseDateOnly
            let! stopDate = this.StopDate |> Option.toResult parseDateOnly
            let! startTime = this.StartTime |> Option.toResult parseTimeOnly
            let! stopTime = this.StopTime |> Option.toResult parseTimeOnly

            return
                { StartDate = startDate
                  StopDate = stopDate
                  StartTime = startTime
                  StopTime = stopTime
                  Workdays = workdays
                  TimeZone = this.TimeZone }
        }
