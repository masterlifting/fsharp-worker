module Domain

module Worker =
    open System
    open Helpers

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

    type WorkDays =
        | Mon
        | Tue
        | Wed
        | Thu
        | Fri
        | Sat
        | Sun

    type TaskScheduler =
        { IsEnabled: bool
          IsOnce: bool
          StartWork: DateTime option
          StopWork: DateTime option
          WorkDays: WorkDays seq
          Delay: TimeSpan
          TimeShift: float }

    type TaskStep = { Name: string; Steps: TaskStep[] }

    type Task =
        { Name: string
          ChunkSize: int
          IsInfinite: bool
          Steps: TaskStep[]
          Scheduler: TaskScheduler }

    let rec convertSteps (steps: Settings.TaskStepSettings[]) : TaskStep[] =
        match steps with
        | [||] -> [||]
        | null -> [||]
        | _ -> steps
        |> Array.map (fun x ->
            { Name = x.Name
              Steps = convertSteps x.Steps })

    let convertToTasks (setting: Settings.Section) : Task seq =
        setting.Tasks
        |> Seq.map (fun x ->
            { Name = x.Key
              ChunkSize = x.Value.ChunkSize
              IsInfinite = x.Value.IsInfinite
              Steps = x.Value.Steps |> convertSteps
              Scheduler =
                { IsEnabled = x.Value.Scheduler.IsEnabled
                  IsOnce = x.Value.Scheduler.IsOnce
                  StartWork = Option.ofNullable x.Value.Scheduler.StartWork
                  StopWork = Option.ofNullable x.Value.Scheduler.StopWork
                  WorkDays =
                    match x.Value.Scheduler.WorkDays.Split(',') with
                    | [||] ->
                        seq {
                            Mon
                            Tue
                            Wed
                            Thu
                            Fri
                            Sat
                            Sun
                        }
                    | workDays ->
                        workDays
                        |> Array.map (function
                            | "mon" -> Mon
                            | "tue" -> Tue
                            | "wed" -> Wed
                            | "thu" -> Thu
                            | "fri" -> Fri
                            | "sat" -> Sat
                            | "sun" -> Sun
                            | _ -> Mon)
                        |> Seq.distinct
                  Delay =
                    match x.Value.Scheduler.Delay with
                    | IsTimeSpan value -> value
                    | _ -> TimeSpan.Zero
                  TimeShift = float x.Value.Scheduler.TimeShift } })
