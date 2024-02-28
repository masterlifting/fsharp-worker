module Infrastructure

open Microsoft.Extensions.Configuration

let getConfig () =
    ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional = false, reloadOnChange = true)
        .Build()

let getConfigSection<'T> (config: IConfigurationRoot) (sectionName: string) =
    config.GetSection(sectionName).Get<'T>()
