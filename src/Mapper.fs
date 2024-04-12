module Worker.Mapper

open Domain.Core
open System

let rec private toList (steps: Domain.Settings.TaskStepSettings array) =
    match steps with
    | [||] -> []
    | null -> []
    | _ ->
        steps
        |> Array.map (fun x ->
            { Name = x.Name
              IsParallel = x.IsParallel
              Steps = x.Steps |> toList })
        |> List.ofArray

let toTask name (settings: Domain.Settings.TaskSettings) =
  { Name = name
    Steps = settings.Steps |> toList
    Scheduler =
      { IsEnabled = settings.Scheduler.IsEnabled
        IsOnce = settings.Scheduler.IsOnce
        StartWork =
          settings.Scheduler.StartWork
          |> Option.ofNullable
          |> Option.defaultValue DateTime.UtcNow
        StopWork = Option.ofNullable settings.Scheduler.StopWork
        WorkDays =
          match settings.Scheduler.WorkDays.Split(',') with
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
          match settings.Scheduler.Delay with
          | Infrastructure.DSL.AP.IsTimeSpan value -> value
          | _ -> TimeSpan.Zero
        TimeShift = settings.Scheduler.TimeShift } }