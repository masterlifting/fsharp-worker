module Worker.Logger

let mutable private logger: Infrastructure.Logging.Logger option = None
let on logger' = logger <- Some logger'

let internal trace =
    match logger with
    | Some logger -> logger.logTrace
    | None -> ignore

let internal debug =
    match logger with
    | Some logger -> logger.logDebug
    | None -> ignore

let internal info =
    match logger with
    | Some logger -> logger.logInfo
    | None -> ignore

let internal warning =
    match logger with
    | Some logger -> logger.logWarning
    | None -> ignore

let internal error =
    match logger with
    | Some logger -> logger.logError
    | None -> ignore
