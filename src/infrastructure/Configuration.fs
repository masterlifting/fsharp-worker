module Configuration

open Microsoft.Extensions.Configuration

let private get () =
    ConfigurationBuilder()
        .AddJsonFile("appsettings.json", optional = false, reloadOnChange = true)
        .Build()

let private settings = get ()

let getSection<'T> name =
    let section = settings.GetSection(name)

    if section.Exists() then section.Get<'T>() |> Some else None
