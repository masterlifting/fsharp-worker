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

    let consoleLogProcessor =
        MailboxProcessor.Start(fun inbox ->
            let rec innerLoop () =
                async {
                    let! getMessage = inbox.Receive()
                    let message = getMessage <| System.DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                    printfn $"{message}"
                    return! innerLoop ()
                }

            innerLoop ())


    let private consoleLog message level =

        match level with
        | Error -> consoleLogProcessor.Post(fun timeStamp -> $"\u001b[31mError [{timeStamp}] {message}\u001b[0m")
        | Warning -> consoleLogProcessor.Post(fun timeStamp -> $"\u001b[33mWarning\u001b[0m [{timeStamp}] {message}")
        | Debug -> consoleLogProcessor.Post(fun timeStamp -> $"\u001b[36mDebug\u001b[0m [{timeStamp}] {message}")
        | Trace -> consoleLogProcessor.Post(fun timeStamp -> $"\u001b[90mTrace\u001b[0m [{timeStamp}] {message}")
        | _ -> consoleLogProcessor.Post(fun timeStamp -> $"\u001b[32mInfo\u001b[0m [{timeStamp}] {message}")

    let Logger =
        match getLevel () with
        | Error ->
            { logTrace = fun _ -> ()
              logDebug = fun _ -> ()
              logInfo = fun _ -> ()
              logWarning = fun _ -> ()
              logError = fun message -> consoleLog message Error }
        | Warning ->
            { logTrace = fun _ -> ()
              logDebug = fun _ -> ()
              logInfo = fun _ -> ()
              logWarning = fun message -> consoleLog message Warning
              logError = fun message -> consoleLog message Error }
        | Information ->
            { logTrace = fun _ -> ()
              logDebug = fun _ -> ()
              logInfo = fun message -> consoleLog message Information
              logWarning = fun message -> consoleLog message Warning
              logError = fun message -> consoleLog message Error }
        | Debug ->
            { logTrace = fun _ -> ()
              logDebug = fun message -> consoleLog message Debug
              logInfo = fun message -> consoleLog message Information
              logWarning = fun message -> consoleLog message Warning
              logError = fun message -> consoleLog message Error }
        | Trace ->
            { logTrace = fun message -> consoleLog message Trace
              logDebug = fun message -> consoleLog message Debug
              logInfo = fun message -> consoleLog message Information
              logWarning = fun message -> consoleLog message Warning
              logError = fun message -> consoleLog message Error }
