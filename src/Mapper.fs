module Worker.Mapper

open System
open Domain.Core
open Infrastructure.Logging
open Infrastructure.DSL.AP
open Infrastructure.Domain.Graph

let private defaultWorkDays =
    set
        [ DayOfWeek.Monday
          DayOfWeek.Tuesday
          DayOfWeek.Wednesday
          DayOfWeek.Thursday
          DayOfWeek.Friday
          DayOfWeek.Saturday
          DayOfWeek.Sunday ]

let private mapSchedule (schedule: Domain.Persistence.Schedule) =
    if not schedule.IsEnabled then
        None
    else
        Some
        <| { StartWork = Option.ofNullable schedule.StartWork |> Option.defaultValue DateTime.UtcNow
             StopWork = Option.ofNullable schedule.StopWork
             WorkDays =
               if schedule.WorkDays = String.Empty then
                   defaultWorkDays
               else
                   match schedule.WorkDays.Split(",") with
                   | [||] -> defaultWorkDays
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
             TimeShift = schedule.TimeShift }

let rec mapTasks (tasks: Domain.Persistence.Task array) =
    match tasks with
    | [||] -> []
    | null -> []
    | _ ->
        tasks
        |> Array.map (fun task ->
            Node(
                { Name = task.Name
                  Parallel = task.Parallel
                  Recursively = task.Recursively
                  Delay =
                    match task.Delay with
                    | IsTimeSpan value -> Some value
                    | _ -> None
                  Duration =
                    match task.Duration with
                    | IsTimeSpan value -> Some value
                    | _ -> None
                  Times = if task.Times = 0 then None else Some(uint task.Times)
                  Schedule = task.Schedule |> mapSchedule },
                task.Steps |> mapTasks
            ))
        |> List.ofArray
