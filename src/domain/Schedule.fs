[<AutoOpen>]
module Worker.Domain.Schedule

open System

type Schedule = {
    Name: string
    StartDate: DateOnly option
    StopDate: DateOnly option
    StartTime: TimeOnly option
    StopTime: TimeOnly option
    Workdays: DayOfWeek Set
    Recursively: TimeSpan option
    TimeZone: uint8
}
