<!-- @format -->

# F# Worker

## Overview

The F# Worker is a powerful tool for managing a graph of tasks and executing them either in parallel or in sequence using scheduling.

## Domain Model

### TaskGraph

The `TaskGraph` represents a task and its configuration. It includes the following properties:

- `Name`: Name of the task.
- `Enabled`: If true, the task is active.
- `Recursively`: (Optional) If specified, the task will be executed recursively based on the provided condition.
- `Parallel`: If true, the task will be executed in parallel with other parallel tasks; otherwise, it will be executed in sequence.
- `Duration`: (Optional) If specified, the task will run for the provided duration.
- `Wait`: If true, the task will wait for dependent tasks to complete.
- `Schedule`: (Optional) The schedule configuration for the task.
- `Tasks`: An array of nested `TaskGraph` elements to define task dependencies.

### Schedule

The `Schedule` represents the scheduling configuration for a task. It includes the following properties:

- `StartDate`: The start date of the task.
- `StopDate`: (Optional) The stop date of the task.
- `StartTime`: The start time of the task.
- `StopTime`: (Optional) The stop time of the task.
- `Workdays`: The days of the week when the task should work (e.g., "mon,tue,wed,thu,fri").
- `TimeShift`: Number of hours to shift the task execution time to UTC (default is 0).

## Dependencies

To use the F# Worker, you need to provide the following dependencies:

```fsharp
type GetTask = string -> Async<Result<Graph.Node<Task>, Error'>>

type WorkerDeps =
  { getTask: GetTask
    Configuration: IConfigurationRoot }
```

## Worker Execution

You can start the worker with the following example:

```fsharp
"Scheduler"
|> Worker.Core.start
  { getTask = taskHandlers |> TasksStorage.getTask configuration
    Configuration = configuration }
|> Async.RunSynchronously
```

In this example:

- "Scheduler" is the name of the task graph to be processed.
- `Worker.Core.start` initializes the worker with the provided dependencies.
- `TasksStorage.getTask` retrieves the task handler based on the provided configuration.
- The worker is then run synchronously using `Async.RunSynchronously`.
