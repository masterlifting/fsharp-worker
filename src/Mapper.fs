module Mapper

open Domain

let toPersistenceTaskStep (step: Core.TaskStepState) : Persistence.StepState =
    { Id = step.Id
      Status =
        match step.Status with
        | Core.TaskStepStatus.Pending -> "Pending"
        | Core.TaskStepStatus.Running -> "Running"
        | Core.TaskStepStatus.Completed -> "Completed"
        | Core.TaskStepStatus.Failed -> "Failed"
      Attempts = step.Attempts
      Message = step.Message
      UpdatedAt = step.UpdatedAt }

let toCoreTaskStep (step: Persistence.StepState) : Core.TaskStepState =
    { Id = step.Id
      Status =
        match step.Status with
        | "Pending" -> Core.TaskStepStatus.Pending
        | "Running" -> Core.TaskStepStatus.Running
        | "Completed" -> Core.TaskStepStatus.Completed
        | "Failed" -> Core.TaskStepStatus.Failed
        | _ -> Core.TaskStepStatus.Failed
      Attempts = step.Attempts
      Message = step.Message
      UpdatedAt = step.UpdatedAt }

let toPersistenceTaskStepJsonString =
    toPersistenceTaskStep >> Serialization.toJsonString

let toCoreTaskStepFromJsonString step =
    match Serialization.fromJsonString<Persistence.StepState> step with
    | Ok step -> toCoreTaskStep step |> Ok
    | Error error -> Error error
