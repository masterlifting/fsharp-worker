module Worker.Mapper

open System
open Domain.Core
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
             TimeShift = schedule.TimeShift
             Delay =
               match schedule.Delay with
               | IsTimeSpan value -> Some value
               | _ -> None
             Limit =
               if schedule.Limit <= 0 then
                   None
               else
                   Some(uint schedule.Limit) }

let private mapTask (task: Domain.Persistence.Task) (handle: TaskHandle) =
    { Name = task.Name
      Parallel = task.Parallel
      Recursively = task.Recursively
      Duration =
        match task.Duration with
        | IsTimeSpan value -> Some value
        | _ -> None
      Schedule = task.Schedule |> mapSchedule
      Handle = handle }

let private getTaskHandle taskName handler =
    match Infrastructure.DSL.Graph.findNode taskName handler with
    | Some handler -> handler.Value.Handle
    | None -> None

let buildGraph (task: Domain.Persistence.Task) handler =

    let rec innerLoop (tasks: Domain.Persistence.Task array) =
        match tasks with
        | [||] -> []
        | null -> []
        | _ ->
            tasks
            |> Array.map (fun task ->
                let handle = getTaskHandle task.Name handler
                let node = mapTask task handle
                Node(node, task.Steps |> innerLoop))
            |> List.ofArray

    let handle = getTaskHandle task.Name handler
    Node(mapTask task handle, task.Steps |> innerLoop)
