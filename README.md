<!-- @format -->

# F# Worker

## Overview

The F# Worker is a powerful tool for managing a graph of tasks and executing them either in parallel or in sequence using scheduling.

## Usage

To use the F# Worker, you need to provide the following:

```yaml
TaskGraph: 
  Name: Name of the task
  Enabled: If true, the task is active
  Recursively: If specified, the task will be executed recursively based on the provided condition
  Parallel: If true, the task will be executed in parallel with other parallel tasks; otherwise, it will be executed in sequence
  Duration: If specified, the task will run for the provided duration
  Wait: If true, the task will wait for dependent tasks to complete
  Schedule: (Optional) The schedule configuration for the task
  Tasks: An array of nested TaskGraph elements to define task dependencies

Schedule:
  StartDate: The start date of the task
  StopDate: (Optional) The stop date of the task
  StartTime: The start time of the task
  StopTime: (Optional) The stop time of the task
  Workdays: The days of the week when the task should work (e.g., "mon,tue,wed,thu,fri")
  TimeShift: Number of hours to shift the task execution time to UTC (default is 0)
```