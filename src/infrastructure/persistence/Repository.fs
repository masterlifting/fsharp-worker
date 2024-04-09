module Repository

let getTask name =
    async { return ConfigurationStorage.getTask name }

let getTasks = ConfigurationStorage.getTasks
