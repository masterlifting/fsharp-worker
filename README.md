<!-- @format -->

# F# Worker

## Overview

The F# Worker is a powerful tool for handling a graph of tasks and executing them either in parallel or in sequence using scheduling.

## Usage

To use the F# Worker, you need to provide the following:

```yaml
Step: 
  Name: Name of the task
  Parallel: If true, the task will be executed in parallel with following parallel tasks, otherwise the task will be executed in sequence
  Recursively: If true, the task will be executed recursively, otherwise the task will be executed only once
  Duration: Duration of the task. We should handle Cancelation token into the handler of the task

Schedule:
  IsEnabled: If true, the task will be scheduled, otherwise the task will be executed immediately
  StartWork: Date and Time when the task should start working (if not specified, the task will start working immediately). Example - "2020-01-01T00:00:00"
  StopWork: Date and Time when the task should stop working (if not specified, the task will work infinitely). Example - "2020-01-01T00:00:00"
  WorkDays: Days of the week when the task should work (if not specified, the task will work every day). Example - "mon,tue,wed,thu,fri,sat,sun"
  Delay: Delay between the task executions. Example -  "00:00:01" - 1 second
  Limit: Number of executions of the task. If not specified, the task will work infinitely.  After the limit is reached, the task will be stopped
  TimeShift: Number of hours to shift the task execution time to UTC. (by default is 0)

```