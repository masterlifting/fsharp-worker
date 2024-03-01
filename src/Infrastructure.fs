module Infrastructure

open Domain.Infrastructure
open Microsoft.Extensions.Configuration

let mutable configuration =
    ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional = false, reloadOnChange = true)
        .Build()

let getConfigSection<'T> sectionName =
    configuration.GetSection(sectionName).Get<'T>()

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

    let reloadCallback _ =
        printfn "Configuration was changed. Reloading..."
        configuration.Reload()

    let a =
        configuration
            .GetReloadToken()
            .RegisterChangeCallback(reloadCallback, configuration)

    let dbContext =
        "ConnectionStrings:WorkerDb" |> getConfigSection<string> |> getDbContext

    { getConfig = fun () -> configuration
      getDbContext = fun () -> dbContext
      getLogger = fun () -> logger }
