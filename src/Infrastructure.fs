module Infrastructure

open Domain.Infrastructure
open Microsoft.Extensions.Configuration

//TODO: Set Logging
let getLogger level = { Level = level }

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
