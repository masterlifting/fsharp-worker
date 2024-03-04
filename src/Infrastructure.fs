module Infrastructure

module Configuration =
    open Microsoft.Extensions.Configuration

    let private get () =
        ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional = false, reloadOnChange = true)
            .Build()

    let private settings = get ()

    let getSection<'T> name =
        let section = settings.GetSection(name)

        if section.Exists() then section.Get<'T>() |> Some else None

module Logging =
    type Logger =
        { logTrace: string -> unit
          logDebug: string -> unit
          logInfo: string -> unit
          logWarning: string -> unit
          logError: string -> unit }

    type private Level =
        | Error
        | Warning
        | Information
        | Debug
        | Trace

    let private getLevel () =
        match Configuration.getSection<string> "Logging:LogLevel:Default" with
        | Some "Error" -> Error
        | Some "Warning" -> Warning
        | Some "Debug" -> Debug
        | Some "Trace" -> Trace
        | _ -> Information

    let private consoleLog message level =
        let getCurrentTimestamp () =
            System.DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")

        match level with
        | Error -> printfn "\u001b[31mError\u001b[0m [%s] %s" (getCurrentTimestamp ()) message
        | Warning -> printfn "\u001b[33mWarning\u001b[0m [%s] %s" (getCurrentTimestamp ()) message
        | Debug -> printfn "\u001b[36mDebug\u001b[0m [%s] %s" (getCurrentTimestamp ()) message
        | Trace -> printfn "\u001b[90mTrace\u001b[0m [%s] %s" (getCurrentTimestamp ()) message
        | _ -> printfn "\u001b[32mInfo\u001b[0m [%s] %s" (getCurrentTimestamp ()) message

    let Logger =
        match getLevel () with
        | Error ->
            { logTrace = fun _ -> ()
              logDebug = fun _ -> ()
              logInfo = fun _ -> ()
              logWarning = fun _ -> ()
              logError = fun m -> consoleLog m Error }
        | Warning ->
            { logTrace = fun _ -> ()
              logDebug = fun _ -> ()
              logInfo = fun _ -> ()
              logWarning = fun m -> consoleLog m Warning
              logError = fun m -> consoleLog m Error }
        | Information ->
            { logTrace = fun _ -> ()
              logDebug = fun _ -> ()
              logInfo = fun m -> consoleLog m Information
              logWarning = fun m -> consoleLog m Warning
              logError = fun m -> consoleLog m Error }
        | Debug ->
            { logTrace = fun _ -> ()
              logDebug = fun m -> consoleLog m Debug
              logInfo = fun m -> consoleLog m Information
              logWarning = fun m -> consoleLog m Warning
              logError = fun m -> consoleLog m Error }
        | Trace ->
            { logTrace = fun m -> consoleLog m Trace
              logDebug = fun m -> consoleLog m Debug
              logInfo = fun m -> consoleLog m Information
              logWarning = fun m -> consoleLog m Warning
              logError = fun m -> consoleLog m Error }
