<!-- @format -->

# F# Worker

## Overview

The F# Worker is a powerful tool for handling a graph of tasks and executing them either in parallel or in sequence using scheduling.

## Usage

To use the F# Worker, you need to provide the following:

- **Worker Configuration**: An instance of the `Domain.Core.WorkerConfiguration` type.

### Worker Configuration

The worker configuration consists of the following components:

- **Task Graph**: A collection of `Domain.Core.TaskSettings` types that define the graph of tasks.

- **Task Handlers**: A collection of `Domain.Core.TaskHandler` types that define the handlers for the tasks in the graph.

- **Duration**: The duration for which the worker should run.

