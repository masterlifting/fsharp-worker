module Worker.Mapper

open Domain.Core
open System
open Infrastructure.Domain

let private mapSchedule (schedule: Domain.Persistence.Schedule option) =
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

let rec mapTasks (tasks: Domain.Persistence.Task array) =
    match tasks with
    | [||] -> []
    | null -> []
    | _ ->
        tasks
        |> Array.map (fun x ->
            Graph ({  Name = x.Name
                      IsParallel = x.IsParallel
                      Schedule = x.Schedule |> mapSchedule }, x.Steps |> mapTasks ))
        |> List.ofArray