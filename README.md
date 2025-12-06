# F# Worker

Task runner for hierarchical workflows with per-node scheduling, recursion, and optional parallel execution. It wires task handlers to a stored task tree (PostgreSQL or configuration) and executes them according to schedule rules.

## Concepts
- **Task tree** (`TaskNode`): Each node can run in parallel with its siblings or sequentially, can repeat (`Recursively` as `TimeSpan`), and can enforce `Duration` and `WaitResult` (wait or fire-and-forget).
- **Handlers** (`WorkerTaskHandler<'a>`): `ActiveTask * 'a * CancellationToken -> Async<Result<unit, Error'>>`. The worker passes your dependency bag `'a` and a cancellation token bounded by `Duration`.
- **Schedule** (`Schedule`): Start/stop date+time, workdays, timezone offset, optional recursive window. See `Worker.Scheduler` for how start/stop/recurrence is computed.
- **Storage**: Task tree can be read from PostgreSQL (with migrations applied automatically) or from configuration. File system and in-memory storages are currently not supported.

## Dependencies
Relies on the shared libs:
- `fsharp-infrastructure`
- `fsharp-persistence`

## Wiring a worker
Create handler tree, optional task tree seed, and start the worker:

```fsharp
open Worker.Client
open Worker.Dependencies
open Worker.Domain
open Infrastructure.Prelude.Tree.Builder

let handlers : Tree.Node<WorkerTaskHandler<_>> =
  Tree.Node.create ("ROOT", Some myHandler)
  |> withChildren [ Tree.Node.create ("CHILD", Some childHandler) ]

let seedTasks : Tree.Node<TaskNode> option = None // or Some tree to insert into DB on startup

let workerDeps : Worker.Dependencies<_> = {
  Name = "MyWorker"
  RootTaskId = "ROOT"
  Storage =
    Persistence.Storage.Connection.Database {
      Database = Persistence.Database.Postgre "Host=..." // connection string
      Lifetime = Persistence.Storage.Lifetime.Scoped
    }
  Tasks = seedTasks
  Handlers = handlers
  TaskDeps = myDependencyBag
}

workerDeps |> Worker.Client.start |> Async.RunSynchronously
```

Notes:
- `Tasks = Some tree` seeds the database (runs migrations, then inserts the tree). Use `None` to rely on already-present tasks.
- `Handlers` must mirror task IDs; missing handlers are skipped, and `WaitResult` controls whether the worker awaits or fire-and-forgets each handler.
- `Recursively` on a task schedules the next run after the given delay once a run completes.

## Scheduling semantics (summary)
- Combines parent and child schedules (intersection of workdays, max of start date/time, min of stop date/time, timezone from child).
- `StartIn/StopIn` responses drive delayed starts/stops; `NotScheduled` skips execution when no schedule exists after merging.
- When `Recursively = true` on the schedule, the worker rolls forward to the next valid window when the current one ends.

## Example
See a concrete usage in `src/embassy-access-worker/Program.fs` of the main repo for how the worker is hosted inside the larger application.