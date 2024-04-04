module Log

type private Logger =
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

let private logger =
    match getLevel () with
    | Error ->
        { logTrace = ignore
          logDebug = ignore
          logInfo = ignore
          logWarning = ignore
          logError = fun message -> consoleLog message Error }
    | Warning ->
        { logTrace = ignore
          logDebug = ignore
          logInfo = ignore
          logWarning = fun message -> consoleLog message Warning
          logError = fun message -> consoleLog message Error }
    | Information ->
        { logTrace = ignore
          logDebug = ignore
          logInfo = fun message -> consoleLog message Information
          logWarning = fun message -> consoleLog message Warning
          logError = fun message -> consoleLog message Error }
    | Debug ->
        { logTrace = ignore
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

let trace = logger.logTrace
let debug = logger.logDebug
let info = logger.logInfo
let warning = logger.logWarning
let error = logger.logError
