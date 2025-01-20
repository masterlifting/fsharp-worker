[<AutoOpen>]
module Worker.Domain.Scheduler

open System

type SchedulerStopReason =
    | NotWorkday of DayOfWeek
    | StopDateReached of DateOnly
    | StopTimeReached of TimeOnly

    member this.Message =
        match this with
        | NotWorkday day -> $"Not workday: {day}"
        | StopDateReached date -> $"Stop date reached: {date}"
        | StopTimeReached time -> $"Stop time reached: {time}"

type Scheduler =
    | NotScheduled
    | Started of Schedule
    | StartIn of TimeSpan * Schedule
    | Stopped of SchedulerStopReason
    | StopIn of TimeSpan * Schedule
