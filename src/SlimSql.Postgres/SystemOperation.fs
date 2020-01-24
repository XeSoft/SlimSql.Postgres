namespace SlimSql.Postgres

module SystemOperation =

    let getTableNames cfg =
        sql
            """
            SELECT c.relname AS TableName
                FROM pg_catalog.pg_class c
                LEFT JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
                WHERE c.relkind IN ('r','')
                AND n.nspname <> 'pg_catalog'
                AND n.nspname <> 'information_schema'
                AND n.nspname !~ '^pg_toast'
                AND pg_catalog.pg_table_is_visible(c.oid)
                ORDER BY 1
            ;
            """
            []
        |> Sql.read<string> cfg
