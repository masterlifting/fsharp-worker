module Configuration

open Microsoft.Extensions.Configuration

let private settings =
    ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional = false, reloadOnChange = true)
        .Build()

let getSection<'T> name =
    let section = settings.GetSection(name)

    if section.Exists() then section.Get<'T>() |> Some else None
