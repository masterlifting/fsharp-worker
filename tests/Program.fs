module Program

open Expecto

[<EntryPoint>]
let main args =
    let allTests =
        testList "All Tests" [ SchedulerTests.schedulerTests; ClientTests.clientTests ]
    runTestsWithCLIArgs [] args allTests
