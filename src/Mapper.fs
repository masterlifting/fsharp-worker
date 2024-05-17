module Worker.Mapper

open System
open Domain.Core
open Infrastructure.Domain.Graph

let private mapSchedule (schedule: Domain.Persistence.Schedule) =
    if not schedule.IsEnabled then
        None
    else
        Some
        <| { StartWork = Option.ofNullable schedule.StartWork |> Option.defaultValue DateTime.UtcNow
             StopWork = Option.ofNullable schedule.StopWork
             WorkDays =
               match schedule.WorkDays.Split(',') with
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
               match schedule.Delay with
               | Infrastructure.DSL.AP.IsTimeSpan value -> value
               | _ -> TimeSpan.Zero
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
                  Recurcive = task.Recurcive
                  Schedule = task.Schedule |> mapSchedule },
                task.Steps |> mapTasks
            ))
        |> List.ofArray
