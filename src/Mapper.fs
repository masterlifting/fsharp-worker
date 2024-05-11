module Worker.Mapper

open Domain.Core
open System

let private mapToSchedule (schedule: Domain.Persistence.Schedule option) =
  schedule
  |> Option.map (fun x ->
    { IsEnabled = x.IsEnabled
      IsOnce = x.IsOnce
      StartWork = Option.ofNullable x.StartWork |> Option.defaultValue DateTime.UtcNow
      StopWork = Option.ofNullable x.StopWork
      WorkDays =
        match x.WorkDays.Split(',') with
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
        match x.Delay with
        | Infrastructure.DSL.AP.IsTimeSpan value -> value
        | _ -> TimeSpan.Zero
      TimeShift = x.TimeShift})

let rec private mapToSteps (steps: Domain.Persistence.Task array) =
    match steps with
    | [||] -> []
    | null -> []
    | _ ->
        steps
        |> Array.map (fun x ->
            { Name = x.Name
              IsParallel = x.IsParallel
              Schedule = x.Schedule |> mapToSchedule 
              Steps = x.Steps |> mapToSteps })
        |> List.ofArray

let mapToTask (task: Domain.Persistence.Task) =
    { Name = task.Name
      IsParallel = task.IsParallel
      Schedule = task.Schedule |> mapToSchedule
      Steps = task.Steps |> mapToSteps }