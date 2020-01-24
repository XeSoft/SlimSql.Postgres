namespace SlimSql.Postgres

module PostgresIdentifier =

    open System.Text.RegularExpressions


    let isValid s =
        Regex.IsMatch(s, """^[A-Za-z_][0-9A-Za-z_]+$""")

