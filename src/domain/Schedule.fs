[<AutoOpen>]
module Worker.Domain.Schedule

open System

type Schedule = {
    StartDate: DateOnly option
    StopDate: DateOnly option
    StartTime: TimeOnly option
    StopTime: TimeOnly option
    Workdays: DayOfWeek Set
    Recursively: bool
    TimeZone: float
}
