module Mapper

open Domain

module TaskStep =
    let toPersistence (step: Core.TaskStepLog) : Persistence.StepState =
        { Id = step.Name
          Status =
            match step.Status with
            | Core.TaskStepStatus.Pending -> "Pending"
            | Core.TaskStepStatus.Running -> "Running"
            | Core.TaskStepStatus.Completed -> "Completed"
            | Core.TaskStepStatus.Failed -> "Failed"
          Attempts = step.Attempts
          Message = step.Message
          UpdatedAt = step.Created }

    let fromPersistence (step: Persistence.StepState) : Core.TaskStepLog =
        { Name = step.Id
          Status =
            match step.Status with
            | "Pending" -> Core.TaskStepStatus.Pending
            | "Running" -> Core.TaskStepStatus.Running
            | "Completed" -> Core.TaskStepStatus.Completed
            | "Failed" -> Core.TaskStepStatus.Failed
            | _ -> Core.TaskStepStatus.Failed
          Attempts = step.Attempts
          Message = step.Message
          Created = step.UpdatedAt }

    let toPersistenceString = toPersistence >> Serialization.toJsonString

    let fromPersistenceString step =
        match Serialization.fromJsonString<Persistence.StepState> step with
        | Ok step -> Ok <| fromPersistence step
        | Error error -> Error error
