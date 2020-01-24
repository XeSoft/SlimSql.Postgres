namespace SlimSql.Postgres

module SqlOperation =

    open System.Text
    open Dapper


    type TmpSqlParam =
        {
            TmpName: string
            OriginalName: string
            Value: obj
            Type: NpgsqlTypes.NpgsqlDbType option
        }


    module TmpSqlParam =
        let fromSqlParam (x: SqlParam) =
            {
                TmpName = x.Name
                OriginalName = x.Name
                Value = x.Value
                Type = x.Type
            }

        let toSqlParam (x: TmpSqlParam) : SqlParam =
            {
                Name = x.TmpName
                Value = x.Value
                Type = x.Type
            }


    /// Merges multiple SqlOperations into a single operation.
    /// An integer is added to the end of each parameter name to ensure it is unique to its own query.
    /// Each statement has its embedded parameters names updated to include the integer.
    /// Recommendations:
    ///   1) Start parameter names with "@" -- Ex: "@MyParameter" -- or some other standard parameter prefix.
    let merge (operations: SqlOperation list) : SqlOperation =
        let transformOp i (op: SqlOperation) =
            let rec transform (sb: StringBuilder) (transformed: SqlParam list) (parms: TmpSqlParam list) =
                match parms with
                | [] -> sb, transformed
                | parm :: nParms ->
                    let startingName = parm.TmpName
                    let endingName = sprintf "%s%i" parm.OriginalName i
                    let nParm =
                        {
                            Name = endingName
                            Value = parm.Value
                            Type = parm.Type
                        }
                    let nSb = sb.Replace(startingName, endingName)
                    // also have to do the same replacement in unprocessed parameter names
                    // to handle case where name is a subset of another parameter name
                    // for example @Email and @EmailId
                    let nParms =
                        nParms
                        |> List.map (fun x ->
                            let nTmpName = x.TmpName.Replace(startingName, endingName)
                            { x with TmpName = nTmpName }
                        )
                    let nTransformed = nParm :: transformed
                    transform nSb nTransformed nParms
            let sb = StringBuilder(op.Statement)
            let parms =
                op.Parameters
                |> List.sortBy (fun x -> x.Name.Length)
                |> List.map TmpSqlParam.fromSqlParam
            transform sb [] parms

        let combine (i, sb: StringBuilder, parms) op =
            let (opSb, opParms) = transformOp i op
            let ni = i + 1
            let nSb = sb.Append(opSb).Append(';')
            let nParms = opParms :: parms
            (ni, nSb, nParms)

        let (_, sb, parms) =
            operations
            |> List.fold combine (0, new StringBuilder(), List.empty)

        {
            Statement = sb.ToString()
            Parameters =
                parms
                |> Seq.concat
                |> Seq.rev
                |> List.ofSeq
        }


    /// Sets the search_path (default schema) to use when executing the query.
    /// Resets the search_path back to default after executing the query.
    /// Make sure the path is a valid Postgres identifier.
    let wrapInSearchPath (path: string) (op: SqlOperation) =
        { op with
            Statement =
                StringBuilder()
                    .AppendFormat("SET search_path TO {0};", path)
                    .AppendLine("")
                    .AppendLine(op.Statement)
                    .AppendLine("SET search_path TO DEFAULT;")
                    .ToString()
        }


    /// Sets the role to use when executing the query.
    /// Resets the role back to default after executing the query.
    /// Make sure the path is a valid Postgres identifier.
    let wrapInRole (role: string) (op: SqlOperation) =
        { op with
            Statement =
                StringBuilder()
                    .AppendFormat("SET ROLE {0};", role)
                    .AppendLine("")
                    .AppendLine(op.Statement)
                    .AppendLine("RESET ROLE;")
                    .ToString()
        }


    let wrapInTransaction (op: SqlOperation) =
        { op with
            Statement =
                StringBuilder()
                    .AppendLine("BEGIN;")
                    .AppendLine(op.Statement)
                    .AppendLine("COMMIT;")
                    .ToString()
        }


    [<AutoOpen>]
    module Helpers =

        open Npgsql


        //////////////
        // Nonsense //
        //////////////
        //
        //   dapper does not accept a list of DbParameters for parameter arg
        let toDapperParams (parms: SqlParam list) : Dapper.SqlMapper.IDynamicParameters =
            { new Dapper.SqlMapper.IDynamicParameters with
                member __.AddParameters(command: System.Data.IDbCommand, _: SqlMapper.Identity): unit =
                    match command with
                    | :? NpgsqlCommand as cmd ->
                        parms
                        |> List.iter (
                            SqlParam.toNpgsqlParam
                            >> cmd.Parameters.Add
                            >> ignore
                        )
                    | _ ->
                        raise (System.NotImplementedException("Expected NpgsqlCommand"))
            }


    let toCommandDefinition (config: SqlConfig) (op: SqlOperation) =
        let parms = toDapperParams op.Parameters
        CommandDefinition(op.Statement, parms, Option.toObj config.Transaction, Option.toNullable config.CommandTimeoutSeconds)



