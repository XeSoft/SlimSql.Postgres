namespace SlimSql.Postgres

module GridReader =

    open FSharp.Control.Tasks


    let read<'T> (mapper : Dapper.SqlMapper.GridReader) =
        task {
            let! resultSeq = mapper.ReadAsync<'T>()
            return List.ofSeq resultSeq
        } |> Async.AwaitTask


    let readFirst<'T> (mapper : Dapper.SqlMapper.GridReader) =
        task {
            let! resultSeq = mapper.ReadAsync<'T>()
            return Seq.tryHead resultSeq
        } |> Async.AwaitTask


