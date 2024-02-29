module Infrastructure

open Domain.Infrastructure
open Microsoft.Extensions.Configuration

//TODO: Set Logging
let getLogger level =
    let lockLog = obj ()

    let getCurrentTimestamp () =
        System.DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")

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

let getConfig () =
    ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional = false, reloadOnChange = true)
        .Build()

let getConfigSection<'T> (config: IConfigurationRoot) sectionName =
    config.GetSection(sectionName).Get<'T>()

let configureWorker () =
    let config = getConfig ()

    let dbContext =
        getConfigSection<string> config "ConnectionStrings:WorkerDb" |> getDbContext

    let logger = getLogger "Info"

    let di =
        { getConfig = fun () -> config
          getDbContext = fun () -> dbContext
          getLogger = fun () -> logger }

    di
