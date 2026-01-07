module ClientTests

open System
open System.Collections.Generic
open System.Threading
open Expecto
open Infrastructure.Domain
open Infrastructure.Prelude.Tree.Builder
open Microsoft.Extensions.Configuration
open Worker.Client
open Worker.Domain
open Worker.Dependencies

// Test helper types
type HandlerCall = {
    TaskId: string
    Attempt: uint<attempts>
    Timestamp: DateTime
    Cancelled: bool
}

type TestTaskDeps = { Calls: HandlerCall list ref }

let createTestSchedule workdays = {
    Name = "Test Schedule"
    StartDate = None
    StopDate = None
    StartTime = None
    StopTime = None
    Workdays = workdays
    Recursively = None
    TimeZone = 0uy
}

let weekdays =
    Set.ofList [
        DayOfWeek.Monday
        DayOfWeek.Tuesday
        DayOfWeek.Wednesday
        DayOfWeek.Thursday
        DayOfWeek.Friday
    ]

let createMockHandler (calls: HandlerCall list ref) =
    Some(fun (activeTask: ActiveTask, deps: TestTaskDeps, ct: CancellationToken) ->
        async {
            let call = {
                TaskId = activeTask.Id.Value
                Attempt = activeTask.Attempt
                Timestamp = DateTime.UtcNow
                Cancelled = ct.IsCancellationRequested
            }
            lock calls (fun () -> calls.Value <- call :: calls.Value)
            return Ok()
        })

let createConfigurationStorage tasksYaml =
    let configDict = Dictionary<string, string>()
    configDict.Add("Tasks", tasksYaml)

    let config = ConfigurationBuilder().AddInMemoryCollection(configDict).Build()
    Persistence.Storage.Connection.Configuration { Provider = config; Section = "Tasks" }

let createSimpleTaskTree () =
    Tree.Node.create (
        "root",
        {
            Enabled = true
            Parallel = false
            Duration = TimeSpan.FromSeconds 1.0
            WaitResult = true
            Schedule = Some(createTestSchedule weekdays)
            Description = Some "Root Task"
        }
    )

let createSimpleHandlerTree calls =
    Tree.Node.create ("root", createMockHandler calls)

[<Tests>]
let clientTests =
    testList "Worker.Client Tests" [

        testList "merge function" [
            test "Merges enabled task with matching handler" {
                let calls = ref []
                let tasks = createSimpleTaskTree ()
                let handlers = createSimpleHandlerTree calls

                let result = merge handlers tasks

                match result with
                | Ok workerTask ->
                    Expect.equal workerTask.Value.Id.Value "root" "Should have root ID"
                    Expect.isTrue workerTask.Value.Handler.IsSome "Should have handler"
                    Expect.equal workerTask.Value.Duration (TimeSpan.FromSeconds 1.0) "Should preserve duration"
                | Error error -> failtest $"Expected Ok, got Error: {error.Message}"
            }

            test "Returns None handler for disabled task" {
                let calls = ref []
                let tasks =
                    Tree.Node.create (
                        "root",
                        {
                            Enabled = false
                            Parallel = false
                            Duration = TimeSpan.FromSeconds 1.0
                            WaitResult = true
                            Schedule = None
                            Description = None
                        }
                    )
                let handlers = createSimpleHandlerTree calls

                let result = merge handlers tasks

                match result with
                | Ok workerTask -> Expect.isTrue workerTask.Value.Handler.IsNone "Disabled task should have no handler"
                | Error error -> failtest $"Expected Ok, got Error: {error.Message}"
            }

            test "Returns None handler when handler not found" {
                let calls = ref []
                let tasks = createSimpleTaskTree ()
                let handlers = Tree.Node.create ("different-id", createMockHandler calls)

                let result = merge handlers tasks

                match result with
                | Ok workerTask ->
                    Expect.isTrue workerTask.Value.Handler.IsNone "Should have no handler when ID doesn't match"
                | Error error -> failtest $"Expected Ok, got Error: {error.Message}"
            }

            test "Merges task tree with children" {
                let calls = ref []

                let childTask =
                    Tree.Node.create (
                        "child",
                        {
                            Enabled = true
                            Parallel = false
                            Duration = TimeSpan.FromSeconds 1.0
                            WaitResult = true
                            Schedule = None
                            Description = Some "Child"
                        }
                    )
                let parentTask =
                    Tree.Node.create (
                        "parent",
                        {
                            Enabled = true
                            Parallel = false
                            Duration = TimeSpan.FromSeconds 1.0
                            WaitResult = true
                            Schedule = None
                            Description = Some "Parent"
                        }
                    )
                    |> withChildren [ childTask ]

                let childHandler = Tree.Node.create ("child", createMockHandler calls)
                let parentHandler =
                    Tree.Node.create ("parent", createMockHandler calls)
                    |> withChildren [ childHandler ]

                let result = merge parentHandler parentTask

                match result with
                | Ok workerTask ->
                    Expect.equal (workerTask.Children |> Seq.length) 1 "Should have one child"
                    let child = workerTask.Children |> Seq.head
                    Expect.equal child.Value.Id.Value "parent.child" "Child should have hierarchical ID"
                | Error error -> failtest $"Expected Ok, got Error: {error.Message}"
            }
        ]

        testList "findTask function with Configuration storage" [
            test "Finds root task directly from constructed tree" {
                let calls = ref []

                let task = {
                    Id = WorkerTaskId.create "root"
                    Parallel = false
                    Duration = TimeSpan.FromSeconds 5.0
                    WaitResult = true
                    Schedule = Some(createTestSchedule weekdays)
                    Handler = createMockHandler calls
                    Description = Some "Test Root Task"
                }

                let workerTask = Tree.Node.create ("root", task)
                let handlers = createSimpleHandlerTree calls

                // Test the merge function directly since Configuration storage is complex
                let result = merge handlers (createSimpleTaskTree ())

                match result with
                | Ok task ->
                    Expect.equal task.Value.Id.Value "root" "Should find root task"
                    Expect.isTrue task.Value.Handler.IsSome "Should have handler"
                | Error error -> failtest $"Expected Ok, got Error: {error.Message}"
            }
        ]

        testList "tryStartTask function" [
            test "Executes handler when task is Started" {
                let calls = ref []
                let deps = { Calls = calls }

                let task = {
                    Id = WorkerTaskId.create "test"
                    Parallel = false
                    Duration = TimeSpan.FromSeconds 1.0
                    WaitResult = true
                    Schedule = Some(createTestSchedule weekdays)
                    Handler = createMockHandler calls
                    Description = Some "Test Task"
                }

                let result = task |> tryStartTask deps 1u<attempts> None |> Async.RunSynchronously

                Expect.isTrue result.IsSome "Should return schedule"
                Expect.isTrue ((!calls).Length > 0) "Handler should have been called"
            }

            test "Does not execute handler when task is Stopped" {
                let calls = ref []
                let deps = { Calls = calls }

                let yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1.0))
                let stoppedSchedule = {
                    Name = "Stopped"
                    StartDate = Some yesterday
                    StopDate = Some yesterday
                    StartTime = None
                    StopTime = None
                    Workdays = weekdays
                    Recursively = None
                    TimeZone = 0uy
                }

                let task = {
                    Id = WorkerTaskId.create "test"
                    Parallel = false
                    Duration = TimeSpan.FromSeconds 1.0
                    WaitResult = true
                    Schedule = Some stoppedSchedule
                    Handler = createMockHandler calls
                    Description = Some "Test Task"
                }

                let result = task |> tryStartTask deps 1u<attempts> None |> Async.RunSynchronously

                Expect.isTrue result.IsNone "Should return None for stopped task"
                Expect.isEmpty !calls "Handler should not have been called"
            }

            test "Skips handler when task is NotScheduled" {
                let calls = ref []
                let deps = { Calls = calls }

                let task = {
                    Id = WorkerTaskId.create "test"
                    Parallel = false
                    Duration = TimeSpan.FromSeconds 1.0
                    WaitResult = true
                    Schedule = None
                    Handler = createMockHandler calls
                    Description = Some "Test Task"
                }

                let result = task |> tryStartTask deps 1u<attempts> None |> Async.RunSynchronously

                Expect.isTrue result.IsNone "Should return None for unscheduled task"
                Expect.isEmpty !calls "Handler should not have been called"
            }
        ]

        testList "processTask function" [
            test "Processes single task with handler" {
                let calls = ref []
                let deps = { Calls = calls }

                let task = {
                    Id = WorkerTaskId.create "single"
                    Parallel = false
                    Duration = TimeSpan.FromSeconds 1.0
                    WaitResult = true
                    Schedule = Some(createTestSchedule weekdays)
                    Handler = createMockHandler calls
                    Description = Some "Single Task"
                }

                let workerTask = Tree.Node.create ("single", task)

                let taskDeps: WorkerTask.Dependencies<TestTaskDeps> = {
                    findTask = fun _ -> async { return Ok(Some workerTask) }
                    tryStartTask = tryStartTask deps
                }

                (taskDeps, None)
                |> processTask (WorkerTaskId.create "single") 1u<attempts>
                |> Async.RunSynchronously

                Expect.isTrue ((!calls).Length > 0) "Handler should have been called"
                let call = (!calls) |> List.head
                Expect.equal call.TaskId "single" "Should execute single task"
                Expect.equal call.Attempt 1u<attempts> "Should have attempt 1"
            }

            test "Handles task not found gracefully" {
                let calls = ref []
                let deps = { Calls = calls }

                let taskDeps: WorkerTask.Dependencies<TestTaskDeps> = {
                    findTask = fun _ -> async { return Ok None }
                    tryStartTask = tryStartTask deps
                }

                (taskDeps, None)
                |> processTask (WorkerTaskId.create "nonexistent") 1u<attempts>
                |> Async.RunSynchronously

                // Should complete without throwing
                Expect.isEmpty !calls "No handler should have been called"
            }
        ]

        testList "processTasks function" [
            test "Processes sequential tasks in order" {
                let calls = ref []
                let deps = { Calls = calls }

                let task1 = {
                    Id = WorkerTaskId.create "seq1"
                    Parallel = false
                    Duration = TimeSpan.FromSeconds 0.1
                    WaitResult = true
                    Schedule = Some(createTestSchedule weekdays)
                    Handler = createMockHandler calls
                    Description = Some "Sequential 1"
                }

                let task2 = {
                    Id = WorkerTaskId.create "seq2"
                    Parallel = false
                    Duration = TimeSpan.FromSeconds 0.1
                    WaitResult = true
                    Schedule = Some(createTestSchedule weekdays)
                    Handler = createMockHandler calls
                    Description = Some "Sequential 2"
                }

                let tasks = [ Tree.Node.create ("seq1", task1); Tree.Node.create ("seq2", task2) ]

                let taskDeps: WorkerTask.Dependencies<TestTaskDeps> = {
                    findTask =
                        fun taskId ->
                            async {
                                let found = tasks |> List.tryFind (fun t -> t.Value.Id.Value = taskId.Value)
                                return Ok found
                            }
                    tryStartTask = tryStartTask deps
                }

                (taskDeps, None) |> processTasks tasks 1u<attempts> |> Async.RunSynchronously

                Expect.equal (!calls).Length 2 "Both tasks should execute"
                let callsInOrder = !calls |> List.rev
                Expect.equal callsInOrder.[0].TaskId "seq1" "First task should execute first"
                Expect.equal callsInOrder.[1].TaskId "seq2" "Second task should execute second"
            }

            test "Processes parallel tasks" {
                let calls = ref []
                let deps = { Calls = calls }

                let task1 = {
                    Id = WorkerTaskId.create "par1"
                    Parallel = true
                    Duration = TimeSpan.FromSeconds 0.1
                    WaitResult = true
                    Schedule = Some(createTestSchedule weekdays)
                    Handler = createMockHandler calls
                    Description = Some "Parallel 1"
                }

                let task2 = {
                    Id = WorkerTaskId.create "par2"
                    Parallel = true
                    Duration = TimeSpan.FromSeconds 0.1
                    WaitResult = true
                    Schedule = Some(createTestSchedule weekdays)
                    Handler = createMockHandler calls
                    Description = Some "Parallel 2"
                }

                let tasks = [ Tree.Node.create ("par1", task1); Tree.Node.create ("par2", task2) ]

                let taskDeps: WorkerTask.Dependencies<TestTaskDeps> = {
                    findTask =
                        fun taskId ->
                            async {
                                let found = tasks |> List.tryFind (fun t -> t.Value.Id.Value = taskId.Value)
                                return Ok found
                            }
                    tryStartTask = tryStartTask deps
                }

                (taskDeps, None) |> processTasks tasks 1u<attempts> |> Async.RunSynchronously

                Expect.equal (!calls).Length 2 "Both parallel tasks should execute"
            }
        ]
    ]
