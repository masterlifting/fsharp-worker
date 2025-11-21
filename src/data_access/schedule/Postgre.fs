module Worker.DataAccess.Postgre.Schedule

open Infrastructure.Prelude
open Persistence.Storages.Postgre
open Persistence.Storages.Domain.Postgre
open Worker.DataAccess

module Query =

    let tryFindById (id: int64) (client: Client) =
        async {
            let request = {
                Sql =
                    """
                    SELECT 
                        id as "Id",
                        start_date as "StartDate",
                        stop_date as "StopDate",
                        start_time as "StartTime",
                        stop_time as "StopTime",
                        workdays as "Workdays",
                        time_zone as "TimeZone"
                    FROM schedules
                    WHERE id = @Id
                """
                Params = Some {| Id = id |}
            }

            return!
                client
                |> Query.get<Schedule.Entity> request
                |> ResultAsync.map Seq.tryHead
                |> ResultAsync.bind (Option.toResult (fun e -> e.ToDomain()))
        }

module Command =

    let create (schedule: Schedule.Entity) (client: Client) =
        async {
            let request = {
                Sql =
                    """
                    INSERT INTO schedules (start_date, stop_date, start_time, stop_time, workdays, time_zone)
                    VALUES (@StartDate, @StopDate, @StartTime, @StopTime, @Workdays, @TimeZone)
                    RETURNING id
                """
                Params = Some schedule
            }

            return!
                client
                |> Query.get<{| Id: int64 |}> request
                |> ResultAsync.map Seq.head
                |> ResultAsync.map _.Id
        }

    let update (schedule: Schedule.Entity) (client: Client) =
        async {
            let request = { Sql = """"""; Params = Some schedule }

            return! client |> Command.execute request |> ResultAsync.map ignore
        }

module Migrations =

    let private initial (client: Client) =
        async {
            let migration = {
                Sql =
                    """
                    CREATE TABLE IF NOT EXISTS schedules (
                        id BIGSERIAL PRIMARY KEY,
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

    let private clean (client: Client) =
        async {
            client |> Provider.dispose
            return Ok()
        }

    let apply (connectionString: string) =
        Provider.init {
            String = connectionString
            Lifetime = Persistence.Domain.Transient
        }
        |> ResultAsync.wrap (fun client -> client |> initial |> ResultAsync.apply (client |> clean))
