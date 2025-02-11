# F# Worker

## Overview
The Worker manages a directed graph of tasks for either parallel or sequential execution with a scheduling system and nested rules. 
It is designed to be used in a distributed environment, such as a microservices architecture.
The Worker just runs the configured task handler that is provided by the user and does not manage the state of the tasks.

### Worker.DataAccess.TaskGraph.TaskGraphEntity
Describes a task node in the task graph, currently supported only through configuration files.

Properties:
- **Id**: Unique identifier of the task.
- **Name**: Descriptive name of the task.
- **Enabled**: Indicates if the task is active.
- **Recursively** (optional): Triggers recursive execution based on a timespan in "dd.hh:mm:ss" format.
- **Parallel**: If true, runs the task in parallel with others; if false, runs in sequence.
- **Duration** (optional): Maximum running time for the task; it is canceled if it exceeds this duration.
- **Wait**: If true, waits for the dependent handler to complete.
- **Schedule** (optional): Scheduling instructions for the task.
- **Tasks**: An array of child tasks (nested TaskGraphEntity items).

### Worker.DataAccess.Schedule.ScheduleEntity
Defines schedule settings for a task. If unspecified, default properties apply.

Properties:
- **StartDate** (optional): Date when the task begins. By default, it starts immediately.
- **StopDate** (optional): Date when the task should stop.
- **StartTime** (optional): Time when the task begins. By default, it starts immediately.
- **StopTime** (optional): Time when the task should stop.
- **Workdays**: Days of the week to run the task (e.g., "mon,tue,wed,thu,fri").
- **TimeZone**: Hours offset from UTC (defaults to 0).

### Worker.Domain.WorkerTaskNodeHandler
Represents a particular task handler.

Properties:
- **Id**: Unique identifier tied to the TaskGraphEntity.
- **Name**: Handler name for the task.
- **Handler**: Function that is executed by the task. Receives the Worker.Domain.WorkerTask, the app configuration, and a cancellation token. Returns a Worker.Domain.WorkerTaskResult.

## Dependencies
This worker depends on my libs:
- **fsharp-infrastructure**
- **fsharp-persistence**

## Worker Execution
To set up the Worker, provide a configuration object such as:

```fsharp
type WorkerConfiguration =
    { RootNodeId: Graph.NodeId
      RootNodeName: string
      Configuration: IConfigurationRoot
      getTaskNode: Graph.NodeId -> Async<Result<Graph.Node<WorkerTaskNode>, Error'>> }
```

You can run the Worker with code like:

```fsharp
let workerConfig =
    { RootNodeId = rootTask.Id
      RootNodeName = rootTask.Name
      Configuration = configuration
      getTaskNode = getTaskNode workerHandlers }

workerConfig |> Worker.start |> Async.RunSynchronously
```

Where:
- **RootNodeId** is the root task node's identifier.
- **RootNodeName** is the root task node's name.
- **Configuration** is the main configuration object.
- **getTaskNode** merged task configuration-based on task definition and task handler to produce a `WorkerTaskNode`.

### [Click here for an example](https://github.com/masterlifting/embassy-access/blob/main/src/embassy-access-worker/Program.fs)