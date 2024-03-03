module Infrastructure

open Domain.Infrastructure
open Microsoft.Extensions.Configuration

let getConfiguration () =
    ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional = false, reloadOnChange = true)
        .Build()

let mutable config = getConfiguration ()

let reloadCallback _ =
    printfn "Configuration was changed. Reloading..."
    config.Reload()

config.GetReloadToken().RegisterChangeCallback(reloadCallback, config) |> ignore

let getConfigSection<'T> sectionName =
    config.GetSection(sectionName).Get<'T>()

let logger =
    let lockLog = obj ()

    let logLevel = getConfigSection<string> "Logging:LogLevel:Default"

    let getCurrentTimestamp () =
        System.DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")

    match logLevel with
    | "Error" ->
        { logInfo = fun _ -> ()
          logWarning = fun _ -> ()
          logError =
            fun message ->
                lock lockLog (fun () -> printfn "\u001b[31mError\u001b[0m [%s] %s" (getCurrentTimestamp ()) message) }
    | "Warning" ->
        { logInfo = fun _ -> ()
          logWarning =
            fun message ->
                lock lockLog (fun () -> printfn "\u001b[33mWarning\u001b[0m [%s] %s" (getCurrentTimestamp ()) message)
          logError =
            fun message ->
                lock lockLog (fun () -> printfn "\u001b[31mError\u001b[0m [%s] %s" (getCurrentTimestamp ()) message) }
    | _ ->
        { logInfo =
            fun message ->
                lock lockLog (fun () -> printfn "\u001b[32mInfo\u001b[0m [%s] %s" (getCurrentTimestamp ()) message)
          logWarning =
            fun message ->
                lock lockLog (fun () -> printfn "\u001b[33mWarning\u001b[0m [%s] %s" (getCurrentTimestamp ()) message)
          logError =
            fun message ->
                lock lockLog (fun () -> printfn "\u001b[31mError\u001b[0m [%s] %s" (getCurrentTimestamp ()) message) }

//TODO: Set Database
let getDbContext connectionString = { ConnectionString = connectionString }

let configureWorker () =

    let dbContext =
        "ConnectionStrings:WorkerDb" |> getConfigSection<string> |> getDbContext

    { getConfig = fun () -> config
      getDbContext = fun () -> dbContext
      getLogger = fun () -> logger }
