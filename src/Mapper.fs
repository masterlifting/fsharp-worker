module Worker.Mapper

open System
open Domain.Core
open Infrastructure.Domain.Graph

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
        |> Array.map (fun task ->
          Node ({  Name = task.Name
                   IsParallel = task.IsParallel
                   Schedule = task.Schedule |> mapSchedule }, task.Steps |> mapTasks ))
        |> List.ofArray