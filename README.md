# SlimSql.Postgres

A minimal PostgreSQL Library for F#.

It doesn't try to hide SQL from you, but just tries to get out of your way while you use SQL. Here's how you can define a query.

# Usage

## Reads

```fsharp
open SlimSql.Postgres

let listCourses offset limit =
    sql
        """
        SELECT CourseId, CourseName
          FROM Course
         LIMIT @Limit
        OFFSET @Offset
        ;
        """
        [
            p "@Offset" offset
            p "@Limit" limit
        ]
```

The helper functions in the above example are `sql` and `p`. Actually, `p` (short for parameter) is just a shortcut for making a tuple. Here's how you would run the query.

```fsharp
type Course =
    {
        CourseId : int
        CourseName : string
    }

let sqlConfig = SqlConfig.create connectStringFromSomewhere
let op = listCourses request.Offset request.Limit
let coursesAsync = Sql.read<Course> sqlConfig op

// coursesAsync is Async<Result<Course list, exn>>
```

Here, `Sql.read<Course>` is a function which runs the operation and converts each data row into a `Course` object. SlimSql uses Dapper under the covers to map into the type you specify. Like with most mappers, the property types and names have to match the returned columns. Additionally, the order of the returned fields from the SQL statement need to match the order of the record fields. 😒


## Writes

```fsharp
let op = sql "UPDATE ..." [ p "@CourseId" courseId ]
Sql.write cfg op

// returns Async<Result<unit, exn>>
```

## Config options

Currently, you are able to specify the following options.

* Command Timeout
* Transaction

```fsharp
let connectString : string = ...
let transaction : IDbTransaction = ...
let timeoutSeconds : int = ...

let config =
    SqlConfig.create connectString
    |> SqlConfig.withTransaction transaction
    |> SqlConfig.withTimeout timeoutSeconds
```

## Merging multiple operations

You can `merge` multiple `SqlOperation` s into a single statement to send them all to the database at once. This optimization allows you to pay the network round-trip latency only once for multiple operations.

### Batching Writes

The example below also adds the transaction statements to the query to ensure that either both statements run successfully or nothing will be changed.

```fsharp
let patches =
    [
        sql "UPDATE ..." [ p "CourseId" courseId ]
        sql "DELETE ..." [ p "CourseId" courseId ]
    ]

patches
|> SqlOperation.merge
|> SqlOperation.wrapInTransaction
|> Sql.write cfg

// returns Async<Result<unit, exn>>
```

### Reads

```fsharp
open SlimSql.Postgres

type Detail =
    {
        CourseCode: string
        CourseName: string
        IsActive: bool
    }
    
type Attachment =
    {
        FileName: string
        Description: string option
        Url: string
    }
    
type Course =
    {
        Detail: Detail
        Attachments: Attachment list
    }

let loadCourseDetail courseId =
    sql
        """
        SELECT CourseCode
             , CourseName
             , IsActive
          FROM Course
         WHERE CourseId = @CourseId
        ;
        """
        [ p "@CourseId" courseId ]
        
let loadCourseAttachments courseId =
    sql
        """
        SELECT FileName
             , Description
             , Url
          FROM CourseAttachment
         WHERE CourseId = @CourseId
        ;
        """
        [ p "@CourseId" courseId ]
                
let op =
    [
        loadCourseDetail courseId
        loadCourseAttachments courseId
    ]
    |> SqlOperation.merge
    
async {
    let sqlConfig = SqlConfig.create connectString
    try
        use! grid = Sql.multiRead sqlConfig op
        let! courseDetail = GridReader.readFirst<Detail> grid
        match courseDetail with
        | None ->
            return Ok None
        | Some detail ->
            let! attachments = GridReader.read<Attachment> grid
            return
                Ok (
                    Some {
                        Detail = detail
                        Attachments = attachments
                    }
                )
    with ex ->
        return Error ex
}

// returns Async<Result<Course option, exn>>
```

## Typed Parameters

Whenever you specify a parameter with `p` as in below, usually Npgsql is pretty good at determining what the corresponding data type should be. However, there are a few common cases that Npgsql cannot determine properly. For those cases, you can use `pTyped` to tell it the database type of the parameter.

### None or null values

Npgsql can never determine what the database type of `null` should be. This also includes `Option` values which have the None case, since at runtime this is actually just `null` as far as Npgsql and Dapper are concerned. (You can observe this yourself in the debugger.) So you have to use `pTyped` whenever you use Option or otherwise have null as a possibility.

```fsharp
open SlimSql.Postgres
open NpgsqlTypes

let searchCourses searchOpt limit offset =
    sql
        """
        SELECT CourseId
             , Name
          FROM Course
             , to_tsquery( @Search ) AS Query
         WHERE IsActive
           AND
             ( @Search IS NULL
            OR TextSearch @@ Query
             )
         ORDER
            BY Name, CourseId
         LIMIT @Limit
        OFFSET @Offset
        ;
        """
        [
            // Must specify the DB type if value could be None or null
            pTyped "@Search" searchOpt NpgsqlDbType.Text
            p "@Limit" limit
            p "@Offset" offset
        ]
```

### Jsonb

In F#, usually JSON or just a string. But Npgsql will always assume strings map to Postgres `text` or something similar. So you have to use `pTyped` to tell it about Jsonb.

_I am not sure if this is required for Postgres `json` type since I have never used it._

```fsharp
open SlimSql.Postgres
open NpgsqlTypes

let createCourse courseId name data isActive =
    sql
        """
        INSERT
          INTO Course
             ( CourseId
             , Name
             , Data
             , IsActive
             )
        VALUES
             ( @CourseId
             , @Name
             , @Data
             , @IsActive
             )
        ;
        """
        [
            p "@CourseId" courseId
            p "@Name" name
            pTyped "@Data" data NpgsqlDbType.Jsonb
            p "@IsActive" isActive
        ]
```

# Installing

Currently, I recommend that you just copy the source files, and add it as a project in your existing solution. The source is small and readable. It targets .NET Standard 2.1 currently, but you should be able to switch to 2.0 if necessary.

# TODO

* Create tests or move tests from the old project
* Add self-contained runnable examples
* Setup build pipeline
* Nuget package
* Improve documentation

# Contributing

Send PRs for documentation improvements.

Create issues for problems that you run into.

Create issues for features that you would like to see added. Bear in mind that this library is intentionally simple and small.

If you want to contribute code, we will have to work through defining the contribution process together.
