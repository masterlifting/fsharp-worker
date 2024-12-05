module Worker.DataAccess.Schedule

open System

type Schedule() =
    member val StartDate: string option = None with get, set
    member val StopDate: string option = None with get, set
    member val StartTime: string option = None with get, set
    member val StopTime: string option = None with get, set
    member val Workdays: string = String.Empty with get, set
    member val TimeZone: int8 = 0y with get, set
