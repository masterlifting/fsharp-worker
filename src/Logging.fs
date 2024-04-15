module Worker.Logger

let mutable private logger: Infrastructure.Logging.Logger option = None
let on logger' = logger <- Some logger'

let internal trace m =
    match logger with
    | Some logger -> logger.logTrace m
    | None -> ()

let internal debug m =
    match logger with
    | Some logger -> logger.logDebug m
    | None -> ()

let internal info m =
    match logger with
    | Some logger -> logger.logInfo m
    | None -> ()

let internal warning m =
    match logger with
    | Some logger -> logger.logWarning m
    | None -> ()

let internal error m =
    match logger with
    | Some logger -> logger.logError m
    | None -> ()
