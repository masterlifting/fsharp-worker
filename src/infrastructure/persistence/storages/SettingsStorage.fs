module SettingsStorage

let private getTasks () =
    match Configuration.getSection<Domain.Settings.Section> "Worker" with
    | Some settings ->
        settings.Tasks
        |> Seq.map (fun taskSettings -> Domain.Core.toTask taskSettings.Key taskSettings.Value)
        |> Ok
    | None -> Error "Worker settings wasnot found"

let getTaskNames () =
    match getTasks () with
    | Ok tasks -> tasks |> Seq.map (fun x -> x.Name) |> Ok
    | Error error -> Error error

let getTask name =
    match getTasks () with
    | Ok tasks ->
        let task = tasks |> Seq.tryFind (fun x -> x.Name = name)

        match task with
        | Some x -> Ok x
        | None -> Error "Task was not found"
    | Error error -> Error error
