module internal Worker.DataAccess.Postgre.TasksTree

open System
open Infrastructure.Domain
open Infrastructure.Prelude
open Persistence.Storages.Postgre
open Persistence.Storages.Domain.Postgre
open Worker.DataAccess

type private TaskNode'() =
    member val Id: string = String.Empty with get, set
    member val ParentId: string | null = null with get, set
    member val Schedule_Name: string | null = null with get, set

    member val Enabled: bool = false with get, set
    member val Recursively: Nullable<TimeSpan> = Nullable() with get, set
    member val Parallel: bool = true with get, set
    member val Duration: TimeSpan = TimeSpan.Zero with get, set
    member val WaitResult: bool = false with get, set
    member val Description: string | null = null with get, set

    member val Schedule_StartDate: string | null = null with get, set
    member val Schedule_StopDate: string | null = null with get, set
    member val Schedule_StartTime: string | null = null with get, set
    member val Schedule_StopTime: string | null = null with get, set
    member val Schedule_Workdays: string | null = null with get, set
    member val Schedule_TimeZone: Nullable<uint8> = Nullable() with get, set

let private toScheduleEntity (row: TaskNode') =
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

let private toNodeEntity (row: TaskNode') =
    TasksTree.NodeEntity(
        Id = row.Id,
        Enabled = row.Enabled,
        Recursively =
            (row.Recursively
             |> Option.ofNullable
             |> Option.map String.fromTimeSpan
             |> Option.toObj),
        Parallel = row.Parallel,
        Duration = (row.Duration |> String.fromTimeSpan),
        WaitResult = row.WaitResult,
        Description = row.Description,
        Schedule = (row |> toScheduleEntity)
    )

module Query =
    open Worker.Domain

    let private result = ResultBuilder()

    let rec private buildTree (nodeId: string) (nodes: TaskNode' seq) =
        result {
            match nodes |> Seq.tryFind (fun r -> r.Id = nodeId) with
            | None -> return! $"Node '{nodeId}' not found in database." |> NotFound |> Error
            | Some node ->
                let entity = node |> toNodeEntity

                let! entityTasks =
                    nodes
                    |> Seq.filter (fun n -> n.ParentId = nodeId)
                    |> Seq.map (fun c -> nodes |> buildTree c.Id)
                    |> Result.choose
                    |> Result.map List.toArray

                entity.Tasks <- entityTasks

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
                |> Query.get<TaskNode'> request
                |> ResultAsync.bind (fun rows ->
                    match rows |> Seq.tryFind (fun r -> r.ParentId |> Option.ofObj |> Option.isNone) with
                    | None -> "Root task node not found in database." |> NotFound |> Error
                    | Some root -> rows |> buildTree root.Id |> Result.bind _.ToDomain())
        }

    let findById (id: WorkerTaskId) (client: Client) =
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
                        tn.description as "Description",
                        
                        s.start_date as "Schedule_StartDate",
                        s.stop_date as "Schedule_StopDate",
                        s.start_time as "Schedule_StartTime",
                        s.stop_time as "Schedule_StopTime",
                        s.workdays as "Schedule_Workdays",
                        s.time_zone as "Schedule_TimeZone"
                    FROM task_nodes as tn
                    LEFT JOIN schedules as s ON s.name = tn.schedule_name
                    WHERE tn.id = @Id OR tn.parent_id = @Id
                    ORDER BY tn.parent_id NULLS FIRST
                """
                Params = Some {| Id = id.Value |}
            }

            return!
                client
                |> Query.get<TaskNode'> request
                |> ResultAsync.bind (fun response ->
                    match response with
                    | [||] -> Ok None
                    | nodes -> nodes |> buildTree id.Value |> Result.bind _.ToDomain() |> Result.map Some)
        }

module Command =
    open Worker.Domain

    let rec insert (tree: Tree.Node<TaskNode>) (client: Client) =
        async {
            let! _ =
                tree.Value.Schedule
                |> Option.map (fun s -> client |> Schedule.Command.insert s)
                |> Option.defaultValue (Ok() |> async.Return)

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
                        @ScheduleName,
                        @Enabled,
                        @Recursively,
                        @Parallel,
                        @Duration,
                        @WaitResult,
                        @Description
                    )
                    ON CONFLICT (id) DO NOTHING;
                """
                Params =
                    Some {|
                        Id = tree.Id.Value
                        ParentId = tree.Parent |> Option.map _.Id.Value |> Option.toObj
                        ScheduleName = tree.Value.Schedule |> Option.map _.Name |> Option.toObj
                        Enabled = tree.Value.Enabled
                        Recursively = tree.Value.Recursively |> Option.toNullable
                        Parallel = tree.Value.Parallel
                        Duration = tree.Value.Duration
                        WaitResult = tree.Value.WaitResult
                        Description = tree.Value.Description |> Option.toObj
                    |}
            }

            match! client |> Command.execute request with
            | Error err -> return Error err
            | Ok _ ->

                let rec insertChildren (nodes: Tree.Node<TaskNode> seq) =
                    async {
                        let nodes = nodes |> Seq.toList
                        match nodes with
                        | [] -> return Ok()
                        | head :: tail ->
                            let! result = insert head client
                            match result with
                            | Error err -> return Error err
                            | Ok _ -> return! insertChildren tail
                    }

                return! insertChildren tree.Children
        }

module Migrations =
    let private resultAsync = ResultAsyncBuilder()

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
                        recursively INTERVAL,
                        parallel BOOLEAN NOT NULL,
                        duration INTERVAL NOT NULL,
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

    let apply client =
        resultAsync { return client |> initial }
