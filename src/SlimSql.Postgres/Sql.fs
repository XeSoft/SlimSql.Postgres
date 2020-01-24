namespace SlimSql.Postgres

module Sql =

    open FSharp.Control.Tasks
    open Npgsql
    open Dapper


    //////////////
    // Nonsense //
    //////////////
    //
    //   b/c dapper type handlers are defined in a singleton
    let private locker = obj()
    let mutable private hasInit = false
    let private checkInit () =
        let init () =
            if hasInit then
                ()
            else
                DapperInit.fSharpTypeHandlers ()
                hasInit <- true

        if hasInit then
            ()
        else
            lock locker init


    let read<'T> (config : SqlConfig) op =
        checkInit ()
        task {
            try
                let cmd = SqlOperation.toCommandDefinition config op
                use connection = new NpgsqlConnection(config.ConnectString)
                let! resultSeq = connection.QueryAsync<'T>(cmd)
                return Ok (List.ofSeq resultSeq)
            with ex ->
                return Error ex
        }
        |> Async.AwaitTask


    let readFirst<'T> (config : SqlConfig) op =
        checkInit ()
        task {
            try
                let cmd = SqlOperation.toCommandDefinition config op
                use connection = new NpgsqlConnection(config.ConnectString)
                let! resultSeq = connection.QueryAsync<'T>(cmd)
                return Ok (Seq.tryHead resultSeq)
            with ex ->
                return Error ex
        }
        |> Async.AwaitTask


    /// make sure to dispose the returned GridReader
    let multiRead (config : SqlConfig) op =
        checkInit ()
        task {
            let cmd = SqlOperation.toCommandDefinition config op
            let connection = new NpgsqlConnection(config.ConnectString)
            return! connection.QueryMultipleAsync(cmd)
        }
        |> Async.AwaitTask


    let multiResult x =
        async {
            try
                let! x_ = x
                return Ok x_
            with ex ->
                return Error ex
        }


    let write (config : SqlConfig) op =
        checkInit ()
        task {
            try
                let cmd = SqlOperation.toCommandDefinition config op
                use connection = new NpgsqlConnection(config.ConnectString)
                let! _ = connection.ExecuteAsync(cmd)
                return Ok ()
            with ex ->
                return Error ex
        }
        |> Async.AwaitTask

