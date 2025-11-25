module Worker.DataAccess.Postgre.TasksTree

open System
open Infrastructure.Domain
open Infrastructure.Prelude
open Persistence.Storages.Postgre
open Persistence.Storages.Domain.Postgre
open Worker.DataAccess

type private TaskNodeRow() =
    member val Id: string = String.Empty with get, set
    member val ParentId: string | null = null with get, set
    member val Schedule_Name: string | null = null with get, set

    member val Enabled: bool = false with get, set
    member val Recursively: string | null = null with get, set
    member val Parallel: bool = true with get, set
    member val Duration: string | null = null with get, set
    member val WaitResult: bool = false with get, set
    member val Description: string | null = null with get, set

    member val Schedule_StartDate: string | null = null with get, set
    member val Schedule_StopDate: string | null = null with get, set
    member val Schedule_StartTime: string | null = null with get, set
    member val Schedule_StopTime: string | null = null with get, set
    member val Schedule_Workdays: string | null = null with get, set
    member val Schedule_TimeZone: Nullable<uint8> = Nullable() with get, set

let private toScheduleEntity (row: TaskNodeRow) =
    row.Schedule_Name
    |> Option.ofObj
    |> Option.map (fun name ->
        Schedule.Entity(
            Name = name,
            StartDate = row.Schedule_StartDate,
            StopDate = row.Schedule_StopDate,
            StartTime = row.Schedule_StartTime,
            StopTime = row.Schedule_StopTime,
            Workdays = row.Schedule_Workdays,
            TimeZone = row.Schedule_TimeZone
        ))
    |> Option.toObj

let private toNodeEntity (row: TaskNodeRow) =
    TasksTree.NodeEntity(
        Id = row.Id,
        Enabled = row.Enabled,
        Recursively = row.Recursively,
        Parallel = row.Parallel,
        Duration = row.Duration,
        WaitResult = row.WaitResult,
        Description = row.Description,
        Schedule = (row |> toScheduleEntity)
    )

module Query =
    let private result = ResultBuilder()

    let rec private buildTree (nodeId: string) (rows: TaskNodeRow seq) =
        result {
            match rows |> Seq.tryFind (fun r -> r.Id = nodeId) with
            | None -> return! $"Node '{nodeId}' not found." |> NotFound |> Error
            | Some row ->
                let entity = row |> toNodeEntity
                let children = rows |> Seq.filter (fun r -> r.ParentId = nodeId)

                let! childEntities =
                    if children |> Seq.isEmpty then
                        Ok [||]
                    else
                        children
                        |> Seq.map (fun child -> rows |> buildTree child.Id)
                        |> Result.choose
                        |> Result.map List.toArray

                entity.Tasks <- childEntities

                return entity
        }

    let get (client: Client) =
        async {
            let request = {
                Sql =
                    """
                    SELECT 
                        tn.id as "Id",
                        tn.parent_id as "ParentId",
                        s.name as "Schedule_Name",
                        
                        tn.enabled as "Enabled",
                        tn.recursively as "Recursively",
                        tn.parallel as "Parallel",
                        tn.duration as "Duration",
                        tn.wait_result as "WaitResult",
                        tn.schedule_id as "ScheduleId",
                        tn.description as "Description",
                        
                        s.start_date as "Schedule_StartDate",
                        s.stop_date as "Schedule_StopDate",
                        s.start_time as "Schedule_StartTime",
                        s.stop_time as "Schedule_StopTime",
                        s.workdays as "Schedule_Workdays",
                        s.time_zone as "Schedule_TimeZone"
                    FROM task_nodes as tn
                    LEFT JOIN schedules as s ON s.name = tn.schedule_name
                    ORDER BY tn.parent_id
                """
                Params = None
            }

            return!
                client
                |> Query.get<TaskNodeRow> request
                |> ResultAsync.bind (fun rows ->
                    match rows |> Seq.tryFind (fun r -> r.ParentId |> Option.ofObj |> Option.isNone) with
                    | None -> "Root task node not found in database." |> NotFound |> Error
                    | Some root -> rows |> buildTree root.Id |> Result.bind _.ToDomain())
        }

    let findById (id: string) (client: Client) =
        async {
            let request = {
                Sql =
                    """
                    WITH RECURSIVE task_tree AS (
                        SELECT 
                            tn.id,
                            tn.parent_id,
                            tn.schedule_name,
                            tn.enabled,
                            tn.recursively,
                            tn.parallel,
                            tn.duration,
                            tn.wait_result,
                            tn.description
                        FROM task_nodes as tn
                        WHERE tn.id = @Id
                        
                        UNION ALL
                        
                        SELECT 
                            tn.id,
                            tn.parent_id,
                            tn.schedule_name,
                            tn.enabled,
                            tn.recursively,
                            tn.parallel,
                            tn.duration,
                            tn.wait_result,
                            tn.description
                        FROM task_nodes as tn
                        INNER JOIN task_tree as tt ON tn.parent_id = tt.id
                    )
                    SELECT 
                        tt.id as "Id",
                        tt.parent_id as "ParentId",
                        s.name as "Schedule_Name",
                        
                        tt.enabled as "Enabled",
                        tt.recursively as "Recursively",
                        tt.parallel as "Parallel",
                        tt.duration as "Duration",
                        tt.wait_result as "WaitResult",
                        tt.description as "Description",
                        
                        s.start_date as "Schedule_StartDate",
                        s.stop_date as "Schedule_StopDate",
                        s.start_time as "Schedule_StartTime",
                        s.stop_time as "Schedule_StopTime",
                        s.workdays as "Schedule_Workdays",
                        s.time_zone as "Schedule_TimeZone"
                    FROM task_tree as tt
                    LEFT JOIN schedules as s ON s.name = tt.schedule_name
                    ORDER BY tt.parent_id
                """
                Params = Some {| Id = id |}
            }

            return!
                client
                |> Query.get<TaskNodeRow> request
                |> ResultAsync.bind (fun rows ->
                    match rows |> Seq.isEmpty with
                    | true -> Ok None
                    | false -> rows |> buildTree id |> Result.bind _.ToDomain() |> Result.map Some)
        }

module Command =
    open Worker.Domain

    let insert (tree: Tree.Node<TaskNode>) (client: Client) =
        let request = {
            Sql =
                """
                INSERT INTO task_nodes (
                    id,
                    parent_id,
                    schedule_name,
                    enabled,
                    recursively,
                    parallel,
                    duration,
                    wait_result,
                    description
                ) VALUES (
                    @Id,
                    @ParentId,
                    @Schedule_Name,
                    @Enabled,
                    @Recursively,
                    @Parallel,
                    @Duration,
                    @WaitResult,
                    @Description
                )
                """
            Params = {
                Id = node.Id
                ParentId = node.ParentId |> Option.toObj
                Schedule_Name = node.Schedule |> Option.map (fun s -> s.Name) |> Option.toObj
                Enabled = node.Enabled
                Recursively = node.Recursively |> Option.toObj
                Parallel = node.Parallel
                Duration = node.Duration |> Option.toObj
                WaitResult = node.WaitResult
                Description = node.Description |> Option.toObj
            }
        }

        client |> Command.execute request

module Migrations =

    let private initial (client: Client) =
        async {
            let migration = {
                Sql =
                    """
                    CREATE TABLE IF NOT EXISTS task_nodes (
                        id TEXT PRIMARY KEY,
                        parent_id TEXT REFERENCES task_nodes(id),
                        schedule_name TEXT REFERENCES schedules(name),

                        enabled BOOLEAN NOT NULL,
                        recursively TEXT,
                        parallel BOOLEAN NOT NULL,
                        duration TEXT,
                        wait_result BOOLEAN NOT NULL,
                        description TEXT
                    );
                    
                    CREATE INDEX IF NOT EXISTS idx_task_nodes_parent_id ON task_nodes(parent_id);
                    CREATE INDEX IF NOT EXISTS idx_task_nodes_schedule_name ON task_nodes(schedule_name);
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
