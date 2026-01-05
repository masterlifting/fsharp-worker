module Worker.DataAccess.Postgre.Schedule

open Infrastructure.Prelude
open Persistence.Storages.Postgre
open Persistence.Storages.Domain.Postgre
open Worker.Domain
open Worker.DataAccess

module Command =
    let insert (schedule: Schedule) (client: Client) =
        match schedule |> Schedule.toEntity with
        | Error e -> e |> async.Return
        | Ok entity ->
            let request = {
                Sql =
                    """
                    INSERT INTO schedules 
                    (name, start_date, stop_date, start_time, stop_time, workdays, recursively, time_zone)
                    VALUES 
                    (@Name, @StartDate, @StopDate, @StartTime, @StopTime, @Workdays, @Recursively, @TimeZone)
                    ON CONFLICT (name) DO NOTHING;
                """
                Params =
                    Some {|
                        Name = entity.Name
                        Workdays = entity.Workdays
                        Recursively = entity.Recursively
                        StartDate = entity.StartDate
                        StopDate = entity.StopDate
                        StartTime = entity.StartTime
                        StopTime = entity.StopTime
                        TimeZone = entity.TimeZone
                    |}
            }

            client |> Command.execute request |> ResultAsync.map ignore

module Migrations =
    let private resultAsync = ResultAsyncBuilder()

    let private initial (client: Client) =
        async {
            let migration = {
                Sql =
                    """
                    CREATE TABLE IF NOT EXISTS schedules (
                        name TEXT PRIMARY KEY,
                        workdays TEXT NULL,
                        recursively INTERVAL NULL,
                        start_date DATE NULL,
                        stop_date DATE NULL,
                        start_time TIME NULL,
                        stop_time TIME NULL,
                        time_zone SMALLINT NOT NULL
                    );
                """
                Params = None
            }

            return! client |> Command.execute migration |> ResultAsync.map ignore
        }

    let internal apply client =
        resultAsync { return client |> initial }
