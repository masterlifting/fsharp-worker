[<AutoOpen>]
module Worker.Domain.Scheduler

open System

type SchedulerStopReason =
    | EmptyWorkdays
    | StopTimeReached of DateTime
    | StartTimeCannotBeReached of DateTime

    member this.Message =
        match this with
        | EmptyWorkdays -> "Empty workdays"
        | StopTimeReached dateTime ->
            let formattedDateTime = dateTime.ToString("yyyy-MM-dd HH:mm:ss")
            $"Stop time reached: {formattedDateTime}"
        | StartTimeCannotBeReached dateTime ->
            let formattedDateTime = dateTime.ToString("yyyy-MM-dd HH:mm:ss")
            $"Start time cannot be reached: {formattedDateTime}"

type Scheduler =
    | NotScheduled
    | Started of Schedule
    | StartIn of TimeSpan * Schedule
    | Stopped of SchedulerStopReason
    | StopIn of TimeSpan * Schedule
