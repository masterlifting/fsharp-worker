[<AutoOpen>]
module Worker.Domain.Schedule

open System

type WorkerSchedule =
    { StartDate: DateOnly option
      StopDate: DateOnly option
      StartTime: TimeOnly option
      StopTime: TimeOnly option
      Workdays: DayOfWeek Set
      TimeZone: int8 }
