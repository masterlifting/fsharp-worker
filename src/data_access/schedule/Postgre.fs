module Worker.DataAccess.Postgre.Schedule

open Infrastructure.Prelude
open Persistence.Storages.Postgre
open Persistence.Storages.Domain.Postgre

module Migrations =

    let private initial (client: Client) =
        async {
            let migration = {
                Sql =
                    """
                    CREATE TABLE IF NOT EXISTS schedules (
                        name TEXT PRIMARY KEY,
                        start_date VARCHAR(50),
                        stop_date VARCHAR(50),
                        start_time VARCHAR(50),
                        stop_time VARCHAR(50),
                        workdays VARCHAR(255),
                        time_zone SMALLINT NOT NULL
                    );
                """
                Params = None
            }

            return! client |> Command.execute migration |> ResultAsync.map ignore
        }

    let apply (connectionString: string) =
        Provider.init {
            String = connectionString
            Lifetime = Persistence.Domain.Transient
        }
        |> ResultAsync.wrap (fun client ->
            client
            |> initial
            |> ResultAsync.apply (client |> Provider.dispose |> Ok |> async.Return))
