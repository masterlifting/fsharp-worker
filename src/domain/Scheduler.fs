[<AutoOpen>]
module Worker.Domain.Scheduler

open System

type SchedulerStopReason =
    | NotWorkday of DayOfWeek
    | StopTimeReached of DateTime

    member this.Message =
        match this with
        | NotWorkday day -> $"Not workday: {day}"
        | StopTimeReached dateTime ->
            let formattedDateTime = dateTime.ToString("yyyy-MM-dd HH:mm:ss")
            $"Stop time reached: {formattedDateTime}"

type Scheduler =
    | NotScheduled
    | Started of Schedule
    | StartIn of TimeSpan * Schedule
    | Stopped of SchedulerStopReason
    | StopIn of TimeSpan * Schedule
