module ConfigurationStorage

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

let getTasks () =
    match Configuration.getSection<Domain.Settings.Section> "Worker" with
    | Some settings ->
        settings.Tasks
        |> Seq.map (fun task ->
            { Name = task.Key
              Steps = task.Value.Steps |> toList
              Scheduler =
                { IsEnabled = task.Value.Scheduler.IsEnabled
                  IsOnce = task.Value.Scheduler.IsOnce
                  StartWork =
                    task.Value.Scheduler.StartWork
                    |> Option.ofNullable
                    |> Option.defaultValue DateTime.UtcNow
                  StopWork = Option.ofNullable task.Value.Scheduler.StopWork
                  WorkDays =
                    match task.Value.Scheduler.WorkDays.Split(',') with
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
                    match task.Value.Scheduler.Delay with
                    | DSL.AP.IsTimeSpan value -> value
                    | _ -> TimeSpan.Zero
                  TimeShift = task.Value.Scheduler.TimeShift } })
        |> Ok
    | None -> Error "Worker settings wasnot found"

let getTask name =
    match getTasks () with
    | Ok tasks ->
        match tasks |> Seq.tryFind (fun x -> x.Name = name) with
        | Some task -> Ok task
        | None -> Error $"Task '%s{name}' was not found"
    | Error error -> Error error
