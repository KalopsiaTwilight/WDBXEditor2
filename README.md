# WDBXEditor 2
### Information
This is a DB2 Editor for the game World of Warcraft.
DB2 definitions come from [WoWDBDefs](https://github.com/wowdev/WoWDBDefs/tree/master/definitions) and uses the [DBCD](https://github.com/wowdev/DBCD) library.

### Requirements
* Visual Studio 2022 (.NET 8)

# WDBX2Sync

This is a CLI tool for importing / exporting DB2 files to SQL / CSV files, allowing you to easily sync tables and files.

## Usage 

**Main**

```
Usage:
  WDBX2Sync [command] [options]

Options:
  --version       Show version information
  -?, -h, --help  Show help and usage information

Commands:
  config                Interact with the configuration file for this tool to provide default arguments.
  export <inputFolder>  Exports data from client files.
  import <inputFolder>  Imports data from a previous export to existing client files.
```

**Export**
```
Usage:
  WDBX2Sync export <inputFolder> [options]

Arguments:
  <inputFolder>  Sets the input directory to source .db2 files from for exporting.

Options:
  -m, --mode <Csv|Mysql|Sqlite>   Sets the output format for the exporter. Possible values are: Csv, Mysql, Sqlite. Default: Csv [default: Csv]
  -o, --output <output>           Sets the output directory where the exported files will be stored. Default: current working directory.
  -cp, --csvPath <csvPath>        Sets the path to specifically write csv files to when mode is set to Csv. Default: output directory.
  -mh, --mysqlHost <mysqlHost>    Sets the hostname to use for MySql connections when mode is set to Mysql. Default: localhost [default: localhost]
  -mp, --mysqlPort <mysqlPort>    Sets the port to use for MySql connections when mode is set to Mysql. Default: 3306 [default: 3306]
  -mdb, --mysqlDb <mysqlDb>       Sets the database to use for MySql connections when mode is set to Mysql. Required when using this mode.
  -mu, --mysqlUser <mysqlUser>    Sets the user to use for MySql connections when mode is set to Mysql. Required when using this mode.
  -mpw, --mysqlPass <mysqlPass>   Sets the password to use for MySql connections when mode is set to Mysql.
  -t, --table <table>             Sets the filenames to export. Multiple --table options can be provided to specify multiple tables. Default: <all .db2 files>
  -sf, --sqliteFile <sqliteFile>  Sets the path for the SQLite database file when mode is set to Sqlite. Required when using this mode.
  -?, -h, --help                  Show help and usage information
```

**Import**
```
Usage:
  WDBX2Sync import <inputFolder> [options]

Arguments:
  <inputFolder>  Sets the input directory to source .db2 files from for importing.

Options:
  -m, --mode <Csv|Mysql|Sqlite>   Sets the operation mode for the exporter. Possible values are: Csv, Mysql, Sqlite. Default: Csv [default: Csv]
  -o, --output <output>           Sets the output directory where the edited files will be stored. Default: override input directory.
  -cp, --csvPath <csvPath>        Sets the path to read import csv files from when mode is set to Csv. Default: current working directory.
  -mh, --mysqlHost <mysqlHost>    Sets the hostname to use for MySql connections when mode is set to Mysql. Default: localhost [default: localhost]
  -mp, --mysqlPort <mysqlPort>    Sets the port to use for MySql connections when mode is set to Mysql. Default: 3306 [default: 3306]
  -mdb, --mysqlDb <mysqlDb>       Sets the database to use for MySql connections when mode is set to Mysql. Required when using this mode.
  -mu, --mysqlUser <mysqlUser>    Sets the user to use for MySql connections when mode is set to Mysql. Required when using this mode.
  -mpw, --mysqlPass <mysqlPass>   Sets the password to use for MySql connections when mode is set to Mysql.
  -t, --table <table>             Sets the table names to import. Multiple --table options can be provided to specify multiple tables. Default: <all .db2 files>
  -sf, --sqliteFile <sqliteFile>  Sets the path for the SQLite database file when mode is set to Sqlite. Required when using this mode.
  -?, -h, --help                  Show help and usage information
```