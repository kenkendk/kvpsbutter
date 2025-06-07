# KVPSButter.Postgres

PostgreSQL backend implementation for KVPSButter.

## Connection String Format

```
postgres://?username=username&password=password&port=port&host=host&tablename=mytable
```

## Supported options:

```
username: The username
password: The password
host: Hostname or IP address of the PostgreSQL server
port: Port of PostgreSQL server, default is 5432
database: Name of the database to connect to, default is empty (connects to the default database)
tablename: Name of the table to be used, default is `kvps`
connectionoptions: Additional connection string options, such as SSL mode, application name, etc.
commandtimeout: The command timeout in seconds, default is 30

```

## Requirements

- PostgreSQL database with a table containing:
  - `keyname` column (text/varchar) - primary key
  - `keyvalue` column (bytea) - stores the value data
  - `size` column (bigint) - stores the size of the value
  - `last_modified` column (timestamp with time zone) - tracks modification time

## Example Table Creation

```sql
CREATE TABLE kvps (
    keyname TEXT PRIMARY KEY,
    keyvalue BYTEA NOT NULL,
    size BIGINT NOT NULL,
    last_modified TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);
```