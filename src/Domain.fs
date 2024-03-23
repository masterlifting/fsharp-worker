module Domain

open System

module Settings =
    open System.Collections.Generic

    [<CLIMutable>]
    type TaskShchedulerSettings =
        { IsEnabled: bool
          IsOnce: bool
          StartWork: Nullable<DateTime>
          StopWork: Nullable<DateTime>
          WorkDays: string
          Delay: string
          TimeShift: byte }

    [<CLIMutable>]
    type TaskStepSettings =
        { Name: string
          Steps: TaskStepSettings[] }

    [<CLIMutable>]
    type TaskSettings =
        { ChunkSize: int
          IsInfinite: bool
          Steps: TaskStepSettings[]
          Scheduler: TaskShchedulerSettings }

    [<CLIMutable>]
    type Section =
        { Tasks: Dictionary<string, TaskSettings> }

module Persistence =
    type IWorkerData =
        abstract CorellationId: Guid option
        abstract StatusId: int
        abstract StepId: int
        abstract Attempts: int
        abstract Error: string option
        abstract UpdatedAt: DateTime

    type Kdmid(corellationId, statusId, stepId, attempts, error, updatedAt) =
        interface IWorkerData with
            member _.CorellationId = corellationId
            member _.StatusId = statusId
            member _.StepId = stepId
            member _.Attempts = attempts
            member _.Error = error
            member _.UpdatedAt = updatedAt

    type Kdmud(corellationId, statusId, stepId, attempts, error, updatedAt) =
        interface IWorkerData with
            member _.CorellationId = corellationId
            member _.StatusId = statusId
            member _.StepId = stepId
            member _.Attempts = attempts
            member _.Error = error
            member _.UpdatedAt = updatedAt

module Core =
    open DSL
    open Settings

    type TaskScheduler =
        { IsEnabled: bool
          IsOnce: bool
          TimeShift: byte
          StartWork: DateTime
          StopWork: DateTime option
          WorkDays: DayOfWeek Set
          Delay: TimeSpan }

    type TaskStep = { Name: string; Steps: TaskStep list }

    type Task =
        { Name: string
          ChunkSize: int
          IsInfinite: bool
          Steps: TaskStep list
          Scheduler: TaskScheduler }

    let rec private toList (steps: TaskStepSettings array) =
        match steps with
        | [||] -> []
        | null -> []
        | _ ->
            steps
            |> Array.map (fun x ->
                { Name = x.Name
                  Steps = toList x.Steps })
            |> List.ofArray

    let toTask name (task: Settings.TaskSettings) =
        { Name = name
          ChunkSize = task.ChunkSize
          IsInfinite = task.IsInfinite
          Steps = task.Steps |> toList
          Scheduler =
            { IsEnabled = task.Scheduler.IsEnabled
              IsOnce = task.Scheduler.IsOnce
              StartWork =
                task.Scheduler.StartWork
                |> Option.ofNullable
                |> Option.defaultValue DateTime.UtcNow
              StopWork = Option.ofNullable task.Scheduler.StopWork
              WorkDays =
                match task.Scheduler.WorkDays.Split(',') with
                | [||] ->
                    set
                        [ DayOfWeek.Friday
                          DayOfWeek.Monday
                          DayOfWeek.Saturday
                          DayOfWeek.Sunday
                          DayOfWeek.Thursday
                          DayOfWeek.Tuesday
                          DayOfWeek.Wednesday ]
                | workDays ->
                    workDays
                    |> Array.map (function
                        | "mon" -> DayOfWeek.Monday
                        | "tue" -> DayOfWeek.Tuesday
                        | "wed" -> DayOfWeek.Wednesday
                        | "thu" -> DayOfWeek.Thursday
                        | "fri" -> DayOfWeek.Friday
                        | "sat" -> DayOfWeek.Saturday
                        | "sun" -> DayOfWeek.Sunday
                        | _ -> DayOfWeek.Sunday)
                    |> Set.ofArray
              Delay =
                match task.Scheduler.Delay with
                | IsTimeSpan value -> value
                | _ -> TimeSpan.Zero
              TimeShift = task.Scheduler.TimeShift } }
