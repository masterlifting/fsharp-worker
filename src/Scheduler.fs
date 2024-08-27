module internal Worker.Scheduler

open System
open System.Threading
open Infrastructure
open Infrastructure.Logging
open Worker.Domain

let private now timeShift =
    DateTime.UtcNow.AddHours(timeShift |> float)

let private checkWorkday
    ((cts: CancellationTokenSource), (task: Task), (timeShift: int8), (workdays: Set<DayOfWeek>), count)
    =
    async {
        let now = now timeShift

        if now.DayOfWeek |> Set.contains >> not <| workdays then
            let message = $"Task '%i{count}.%s{task.Name}'. Today is not a working day."

            match task.Recursively with
            | None ->
                message |> Log.warning
                do! cts.CancelAsync() |> Async.AwaitTask
            | Some _ ->
                let delay = now.Date.AddDays(1.) - now
                $"{message} Will be started in {delay}." |> Log.warning
                do! Async.Sleep delay
    }

let private checkLimit
    ((cts: CancellationTokenSource), (task: Task), (timeShift: int8 option), (limit: uint option), count)
    =
    async {
        match limit with
        | Some limit when count % (limit + 1u) = 0u ->
            let message = $"Task '%i{count}.%s{task.Name}'. Limit {limit} reached"

            match task.Recursively with
            | None ->
                $"{message}." |> Log.warning
                do! cts.CancelAsync() |> Async.AwaitTask
            | Some _ ->
                let timeShift =
                    match timeShift with
                    | Some timeShift -> timeShift
                    | None ->
                        $"Task '%i{count}.%s{task.Name}'. TimeShift is not defined. Default value 0 will be used."
                        |> Log.warning

                        0y

                let now = now timeShift
                let delay = now.Date.AddDays(1.) - now
                let formattedDelay = delay.ToString("dd\\d\\ hh\\h\\ mm\\m\\ ss\\s")

                $"{message} for today. New limit will be available in {formattedDelay}."
                |> Log.warning

                do! Async.Sleep delay
        | _ -> ()
    }

let private tryStopWork
    ((cts: CancellationTokenSource), (task: Task), (timeShift: int8), (stopWork: DateTime option), count)
    =
    async {
        match stopWork with
        | Some stopWork ->
            match stopWork - now timeShift with
            | delay when delay > TimeSpan.Zero ->
                $"Task '%i{count}.%s{task.Name}'. Will be stopped at {stopWork}." |> Log.warning
                cts.CancelAfter delay
            | _ -> do! cts.CancelAsync() |> Async.AwaitTask
        | _ -> ()
    }

let private tryStartWork ((task: Task), (timeShift: int8), (startWork: DateTime), count) =
    async {
        match startWork - now timeShift with
        | delay when delay > TimeSpan.Zero ->
            $"Task '%i{count}.%s{task.Name}'. Will be started at {startWork}."
            |> Log.warning

            do! Async.Sleep delay
        | _ -> ()
    }

let getExpirationToken (task: Task) count (cts: CancellationTokenSource) =
    async {
        match task.Schedule with
        | None -> do! checkLimit (cts, task, None, task.Limit, count)
        | Some schedule ->

            do! checkLimit (cts, task, Some schedule.TimeShift, task.Limit, count)

            if cts.Token |> notCanceled then
                do! tryStopWork (cts, task, schedule.TimeShift, schedule.StopWork, count)

            if cts.Token |> notCanceled then
                do! checkWorkday (cts, task, schedule.TimeShift, schedule.Workdays, count)

            if cts.Token |> notCanceled then
                do! tryStartWork (task, schedule.TimeShift, schedule.StartWork, count)

        return cts.Token
    }
