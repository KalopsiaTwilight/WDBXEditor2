using DBCD.Providers;
using Microsoft.Data.Sqlite;
using MySql.Data.MySqlClient;
using System.CommandLine;
using System.CommandLine.Binding;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Text.Json;
using WDBXEditor2.Core.Operations;
using WDBXEditor2.Tools.Core;
using WDBXEditor2.Tools.Core.Config;

namespace WDBXEditor2.Tools.WDBX2Sync
{
    internal class Program
    {
        private ImportExportConfigBinder? exportConfigBinder;
        private ImportExportConfigBinder? importConfigBinder;

        static async Task<int> Main(string[] args)
        {
            var rootCmd = new RootCommand();

            var program = new Program();
            program.SetUpCommands(rootCmd);

            var returnCode = await rootCmd.InvokeAsync(args);
            return returnCode;
        }

        void SetUpCommands(RootCommand rootCmd)
        {
            var configCmd = new Command("config", "Interact with the configuration file for this tool to provide default arguments.");
            rootCmd.Add(configCmd);
            var configInitCmd = new Command("init", "Initializes a new config file.");
            configCmd.Add(configInitCmd);
            configInitCmd.SetHandler(HandleConfigInit);
            var configEditCmd = new Command("edit", "Opens the current config file for editing.");
            configCmd.Add(configEditCmd);
            configEditCmd.SetHandler(HandleConfigEdit);

            var exportCmd = new Command("export", "Exports data from client files.") { };
            rootCmd.Add(exportCmd);
            exportCmd.SetHandler(HandleExport);

            var inputPathArg = new Argument<string>("inputFolder", "Sets the input directory to source .db2 files from for exporting.");
            exportCmd.AddArgument(inputPathArg);

            var exportModeOpt = new Option<Mode>(["--mode", "-m"], () => Mode.Csv,
                $"Sets the output format for the exporter. Possible values are: {string.Join(", ", Enum.GetNames(typeof(Mode)))}. Default: {nameof(Mode.Csv)}"
            );
            exportCmd.AddOption(exportModeOpt);
            var exportOutputPathOpt = new Option<string>(["--output", "-o"],
                "Sets the output directory where the exported files will be stored. Default: current working directory."
            );
            exportCmd.AddOption(exportOutputPathOpt);
            var exportCsvPathOpt = new Option<string>(["--csvPath", "-cp"],
                $"Sets the path to specifically write csv files to when mode is set to {nameof(Mode.Csv)}. Default: output directory.");
            exportCmd.AddOption(exportCsvPathOpt);
            var exportSqlDropTableOpt = new Option<bool>("--dropTables", () => true,
                "Determines wether or not to use a DROP TABLE IF EXISTS command before inserting data. Default: true")
            { IsHidden = true };
            exportCmd.AddOption(exportSqlDropTableOpt);
            var exportSqlCreateTableOpt = new Option<bool>("--createTables", () => true,
                "Determines wether or not to use a CREATE TABLE command before inserting data. Default: true")
            { IsHidden = true };
            exportCmd.AddOption(exportSqlCreateTableOpt);
            var exportSqlExportDataOpt = new Option<bool>("--exportData", () => true,
                "Determines wether or not to insert data from the table. Default: true")
            { IsHidden = true };
            exportCmd.AddOption(exportSqlExportDataOpt);
            var exportSqlInsertsPerTableOpt = new Option<uint>("--inserts", () => 1000,
                "Sets the amount of values to insert per executed query. Default: 1000")
            { IsHidden = true };
            exportCmd.AddOption(exportSqlInsertsPerTableOpt);
            var exportSqlTableFormatterOpt = new Option<string>(["--tableFormat", "-tf"],
                "Sets the format string to use for table name generation. I.e. '--tableFormat {0}_wdbx' could be used to create table names like achievement_wdbx for the Achievement.db2")
            { IsHidden = true };
            exportCmd.AddOption(exportSqlTableFormatterOpt);
            var exportMysqlDatabaseHostOpt = new Option<string>(["--mysqlHost", "-mh"], () => "localhost",
               $"Sets the hostname to use for MySql connections when mode is set to {nameof(Mode.Mysql)}. Default: localhost");
            exportCmd.AddOption(exportMysqlDatabaseHostOpt);
            var exportMysqlDatabasePortOpt = new Option<uint>(["--mysqlPort", "-mp"], () => 3306,
               $"Sets the port to use for MySql connections when mode is set to {nameof(Mode.Mysql)}. Default: 3306");
            exportCmd.AddOption(exportMysqlDatabasePortOpt);
            var exportMysqlDatabaseNameOpt = new Option<string>(["--mysqlDb", "-mdb"],
               $"Sets the database to use for MySql connections when mode is set to {nameof(Mode.Mysql)}. Required when using this mode.");
            exportCmd.AddOption(exportMysqlDatabaseNameOpt);
            var exportMysqlDatabaseUserOpt = new Option<string>(["--mysqlUser", "-mu"],
               $"Sets the user to use for MySql connections when mode is set to {nameof(Mode.Mysql)}. Required when using this mode.");
            exportCmd.AddOption(exportMysqlDatabaseUserOpt);
            var exportMysqlDatabasePassOpt = new Option<string>(["--mysqlPass", "-mpw"],
               $"Sets the password to use for MySql connections when mode is set to {nameof(Mode.Mysql)}.");
            exportCmd.AddOption(exportMysqlDatabasePassOpt);
            var exportTablesOpt = new Option<string[]>(["--table", "-t"],
                "Sets the filenames to export. Multiple --table options can be provided to specify multiple tables. Default: <all .db2 files>");
            exportCmd.AddOption(exportTablesOpt);
            var exportSqliteFileOpt = new Option<string>(["--sqliteFile", "-sf"],
                $"Sets the path for the SQLite database file when mode is set to {nameof(Mode.Sqlite)}. Required when using this mode.");
            exportCmd.AddOption(exportSqliteFileOpt);

            exportConfigBinder = new ImportExportConfigBinder(inputPathArg, exportModeOpt, exportOutputPathOpt,
                exportSqlDropTableOpt, exportSqlCreateTableOpt, exportSqlExportDataOpt, exportSqlInsertsPerTableOpt, exportSqlTableFormatterOpt,
                exportMysqlDatabaseHostOpt, exportMysqlDatabasePortOpt, exportMysqlDatabaseNameOpt, exportMysqlDatabaseUserOpt, exportMysqlDatabasePassOpt,
                exportTablesOpt, exportSqliteFileOpt, exportCsvPathOpt);


            var importCmd = new Command("import", "Imports data from a previous export to existing client files.") { };
            rootCmd.Add(importCmd);
            importCmd.SetHandler(HandleImport);

            var importInputPathArg = new Argument<string>("inputFolder", "Sets the input directory to source .db2 files from for importing.");
            importCmd.AddArgument(importInputPathArg);
            var importMode = new Option<Mode>(["--mode", "-m"], () => Mode.Csv,
                $"Sets the operation mode for the exporter. Possible values are: {string.Join(", ", Enum.GetNames(typeof(Mode)))}. Default: {nameof(Mode.Csv)}"
            );
            importCmd.AddOption(importMode);
            var importOutputPathOpt = new Option<string>(["--output", "-o"],
                "Sets the output directory where the edited files will be stored. Default: override input directory."
            );
            importCmd.AddOption(importOutputPathOpt);
            var importCsvPathOpt = new Option<string>(["--csvPath", "-cp"],
                $"Sets the path to read import csv files from when mode is set to {nameof(Mode.Csv)}. Default: current working directory.");
            importCmd.AddOption(importCsvPathOpt);
            var importSqlTableFormatterOpt = new Option<string>(["--tableFormat", "-tf"],
                "Sets the format string to use for table name generation. I.e. '--tableFormat {0}_wdbx' could be used to create table names like achievement_wdbx for the Achievement.db2")
            { IsHidden = true };
            var importMysqlDatabaseHostOpt = new Option<string>(["--mysqlHost", "-mh"], () => "localhost",
               $"Sets the hostname to use for MySql connections when mode is set to {nameof(Mode.Mysql)}. Default: localhost");
            importCmd.AddOption(importMysqlDatabaseHostOpt);
            var importMysqlDatabasePortOpt = new Option<uint>(["--mysqlPort", "-mp"], () => 3306,
               $"Sets the port to use for MySql connections when mode is set to {nameof(Mode.Mysql)}. Default: 3306");
            importCmd.AddOption(importMysqlDatabasePortOpt);
            var importMysqlDatabaseNameOpt = new Option<string>(["--mysqlDb", "-mdb"],
               $"Sets the database to use for MySql connections when mode is set to {nameof(Mode.Mysql)}. Required when using this mode.");
            importCmd.AddOption(importMysqlDatabaseNameOpt);
            var importMysqlDatabaseUserOpt = new Option<string>(["--mysqlUser", "-mu"],
               $"Sets the user to use for MySql connections when mode is set to {nameof(Mode.Mysql)}. Required when using this mode.");
            importCmd.AddOption(importMysqlDatabaseUserOpt);
            var importMysqlDatabasePassOpt = new Option<string>(["--mysqlPass", "-mpw"],
               $"Sets the password to use for MySql connections when mode is set to {nameof(Mode.Mysql)}.");
            importCmd.AddOption(importMysqlDatabasePassOpt);
            var importTablesOpt = new Option<string[]>(["--table", "-t"],
                "Sets the table names to import. Multiple --table options can be provided to specify multiple tables. Default: <all .db2 files>");
            importCmd.AddOption(importTablesOpt);
            var importSqliteFileOpt = new Option<string>(["--sqliteFile", "-sf"],
                $"Sets the path for the SQLite database file when mode is set to {nameof(Mode.Sqlite)}. Required when using this mode.");
            importCmd.AddOption(importSqliteFileOpt);

            importConfigBinder = new ImportExportConfigBinder(importInputPathArg, importMode, importOutputPathOpt, importSqlTableFormatterOpt,
                importMysqlDatabaseHostOpt, importMysqlDatabasePortOpt, importMysqlDatabaseNameOpt, importMysqlDatabaseUserOpt, importMysqlDatabasePassOpt,
                importTablesOpt, importSqliteFileOpt, importCsvPathOpt);
        }

        void HandleExport(InvocationContext context)
        {
            if (exportConfigBinder == null)
            {
                throw new InvalidOperationException("HandleExport called before commands were set up.");
            }

            (exportConfigBinder as IValueSource).TryGetValue(exportConfigBinder, context.BindingContext, out var cmdConfigObj);


            var config = new ImportExportConfig();

            var configFilePath = GetConfigFilePath();
            if (File.Exists(configFilePath))
            {
                var fileConfig = JsonSerializer.Deserialize<ImportExportConfig>(File.ReadAllText(configFilePath));
                if (fileConfig == null)
                {
                    throw new Exception("Unable to read data from config file.");
                }
                config.Merge(fileConfig);
            }

            if (cmdConfigObj is ImportExportConfig cmdConfig)
            {
                config.Merge(cmdConfig);
            }

            var exitCode = 0;
            switch (config.Mode)
            {
                case Mode.Csv: exitCode = HandleCsvExport(context.Console, config, context.GetCancellationToken()); break;
                case Mode.Mysql: exitCode = HandleMySqlExport(context.Console, config, context.GetCancellationToken()); break;
                case Mode.Sqlite: exitCode = HandleSqliteExport(context.Console, config, context.GetCancellationToken()); break;
            }

            context.ExitCode = exitCode;
        }

        void HandleImport(InvocationContext context)
        {
            if (importConfigBinder == null)
            {
                throw new InvalidOperationException("HandleImport called before commands were set up.");
            }

            (importConfigBinder as IValueSource).TryGetValue(importConfigBinder, context.BindingContext, out var cmdConfigObj);

            var config = new ImportExportConfig();

            var configFilePath = GetConfigFilePath();
            if (File.Exists(configFilePath))
            {
                var fileConfig = JsonSerializer.Deserialize<ImportExportConfig>(File.ReadAllText(configFilePath));
                if (fileConfig == null)
                {
                    throw new Exception("Unable to read data from config file.");
                }
                config.Merge(fileConfig);
            }

            if (cmdConfigObj is ImportExportConfig cmdConfig)
            {
                config.Merge(cmdConfig);
            }

            var exitCode = 0;
            switch (config.Mode)
            {
                case Mode.Csv: exitCode = HandleCsvImport(context.Console, config, context.GetCancellationToken()); break;
                case Mode.Mysql: exitCode = HandleMySqlImport(context.Console, config, context.GetCancellationToken()); break;
                case Mode.Sqlite: exitCode = HandleSqliteImport(context.Console, config, context.GetCancellationToken()); break;
            }

            context.ExitCode = exitCode;
        }

        int HandleCsvExport(IConsole console, ImportExportConfig config, CancellationToken cancellationToken)
        {
            var outputFolder = config.CsvPath ?? config.OutputPath ?? "";
            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            var handler = new ExportToCsvOperationHandler();
            var dbcd = GetDBCD(config);
            var fileNames = GetFileNames(config);

            foreach (var file in fileNames)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return 2;
                }

                console.WriteLine($"Processing {file}...");
                if (!File.Exists(file))
                {
                    console.WriteLine($"File: '{file}' not found. Skipping...");
                    continue;
                }

                var tableName = Path.GetFileNameWithoutExtension(file);
                var storage = dbcd.Load(tableName);

                var outputPath = Path.Join(outputFolder, tableName + ".csv");
                using var progressReporter = new ConsoleProgressReporter(console);
                handler.Handle(new ExportToCsvOperation()
                {
                    Storage = storage,
                    FileName = outputPath,
                    ProgressReporter = progressReporter
                }, cancellationToken).Wait(cancellationToken);
            }
            return 0;
        }

        int HandleMySqlExport(IConsole console, ImportExportConfig config, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(config.MySqlDatabaseHost))
            {
                console.WriteLine($"No MySql database host provided. This option is required when using output mode {nameof(Mode.Mysql)}");
                return 1;
            }
            if (string.IsNullOrEmpty(config.MySqlDatabaseName))
            {
                console.WriteLine($"No MySql database name provided. This option is required when using output mode {nameof(Mode.Mysql)}");
                return 1;
            }
            if (string.IsNullOrEmpty(config.MySqlDatabaseUser))
            {
                console.WriteLine($"No MySql database user provided. This option is required when using output mode {nameof(Mode.Mysql)}");
                return 1;
            }

            var handler = new ExportToSqlOperationsHandler();
            var dbcd = GetDBCD(config);
            var fileNames = GetFileNames(config);

            foreach (var file in fileNames)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return 2;
                }

                console.WriteLine($"Processing {file}...");
                if (!File.Exists(file))
                {
                    console.WriteLine($"File: '{file}' not found. Skipping...");
                    continue;
                }

                var tableName = Path.GetFileNameWithoutExtension(file);
                var storage = dbcd.Load(tableName);

                using var progressReporter = new ConsoleProgressReporter(console);
                handler.Handle(new ExportToMysqlDatabaseOperation()
                {
                    Storage = storage,
                    CreateTable = config.SqlCreateTable,
                    DropTable = config.SqlDropTable,
                    ExportData = config.SqlExportData,
                    InsertsPerStatement = config.SqlInsertsPerTable,
                    ProgressReporter = progressReporter,
                    TableName = string.Format(config.SqlTableFormatter ?? "{0}", tableName.ToLower()),
                    DatabaseHost = config.MySqlDatabaseHost,
                    DatabaseName = config.MySqlDatabaseName,
                    DatabasePassword = config.MySqlDatabasePassword ?? string.Empty,
                    DatabasePort = config.MySqlDatabasePort,
                    DatabaseUser = config.MySqlDatabaseUser,
                }, cancellationToken).Wait(cancellationToken);
            }
            return 0;
        }

        int HandleSqliteExport(IConsole console, ImportExportConfig config, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(config.SqliteDatabaseFile))
            {
                console.WriteLine($"No SQLite database file provided. This option is required when using output mode {nameof(Mode.Sqlite)}");
                return 1;
            }

            var dbFolder = Path.GetDirectoryName(config.SqliteDatabaseFile);
            if (!string.IsNullOrEmpty(dbFolder) && !Directory.Exists(dbFolder))
            {
                Directory.CreateDirectory(dbFolder);
            }

            var handler = new ExportToSqlOperationsHandler();
            var dbcd = GetDBCD(config);
            var fileNames = GetFileNames(config);

            foreach (var file in fileNames)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return 2;
                }

                console.WriteLine($"Processing {file}...");
                if (!File.Exists(file))
                {
                    console.WriteLine($"File: '{file}' not found. Skipping...");
                    continue;
                }

                var tableName = Path.GetFileNameWithoutExtension(file);
                var storage = dbcd.Load(tableName);

                using var progressReporter = new ConsoleProgressReporter(console);
                handler.Handle(new ExportToSQLiteDatabaseOperation()
                {
                    Storage = storage,
                    CreateTable = config.SqlCreateTable,
                    DropTable = config.SqlDropTable,
                    ExportData = config.SqlExportData,
                    InsertsPerStatement = config.SqlInsertsPerTable,
                    ProgressReporter = progressReporter,
                    TableName = string.Format(config.SqlTableFormatter ?? "{0}", tableName.ToLower()),
                    FileName = config.SqliteDatabaseFile
                }, cancellationToken).Wait(cancellationToken);
            }
            return 0;
        }

        int HandleCsvImport(IConsole console, ImportExportConfig config, CancellationToken cancellationToken)
        {
            var csvInputDirectory = config.CsvPath ?? "";
            if (!Directory.Exists(csvInputDirectory))
            {
                console.WriteLine("Input directory for csv files does not exist!");
                return -1;
            }

            var outputPath = config.OutputPath ?? "";
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            var handler = new ImportFromCsvOperationHandler();
            var dbcd = GetDBCD(config);
            var fileNames = GetFileNames(config);

            foreach (var file in fileNames)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return 2;
                }

                console.WriteLine($"Processing {file}...");
                if (!File.Exists(file))
                {
                    console.WriteLine($"File: '{file}' not found. Skipping...");
                    continue;
                }
                var tableName = Path.GetFileNameWithoutExtension(file);
                var csvFilePath = Path.Combine(csvInputDirectory, tableName + ".csv");
                if (!File.Exists(csvFilePath))
                {
                    console.WriteLine($"File: '{csvFilePath}' not found. Skipping...");
                    continue;
                }

                using var progressReporter = new ConsoleProgressReporter(console);
                var storage = dbcd.Load(tableName);
                handler.Handle(new ImportFromCsvOperation()
                {
                    Storage = storage,
                    FileName = csvFilePath,
                    ProgressReporter = progressReporter
                }, cancellationToken).Wait(cancellationToken);
                storage.Save(Path.Join(outputPath, tableName + ".db2"));
            }
            return 0;
        }

        int HandleMySqlImport(IConsole console, ImportExportConfig config, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(config.MySqlDatabaseHost))
            {
                console.WriteLine($"No MySql database host provided. This option is required when using output mode {nameof(Mode.Mysql)}");
                return 1;
            }
            if (string.IsNullOrEmpty(config.MySqlDatabaseName))
            {
                console.WriteLine($"No MySql database name provided. This option is required when using output mode {nameof(Mode.Mysql)}");
                return 1;
            }
            if (string.IsNullOrEmpty(config.MySqlDatabaseUser))
            {
                console.WriteLine($"No MySql database user provided. This option is required when using output mode {nameof(Mode.Mysql)}");
                return 1;
            }


            var outputPath = config.OutputPath ?? "";
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            List<string> existingTables = new();
            console.WriteLine($"Reading existing tables...");
            try
            {
                var connectionBuilder = new MySqlConnectionStringBuilder
                {
                    Server = config.MySqlDatabaseHost,
                    Database = config.MySqlDatabaseName,
                    Password = config.MySqlDatabasePassword ?? string.Empty,
                    Port = config.MySqlDatabasePort,
                    UserID = config.MySqlDatabaseUser,
                };
                var sqlConnection = new MySqlConnection(connectionBuilder.ConnectionString);
                var command = sqlConnection.CreateCommand();
                command.CommandText = "SHOW TABLES;";
                sqlConnection.Open();
                var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    existingTables.Add(reader.GetString(0));
                }
                sqlConnection.Close();
            }
            catch (MySqlException ex)
            {
                if (ex.Number == 1045)
                {
                    console.WriteLine("Error: invalid authentication provided for Mysql connection.");
                    return 1;
                }
                else
                {
                    throw;
                }
            }

            var handler = new ImportFromSqlOperationsHandler();
            var fileNames = GetFileNames(config);
            var dbcd = GetDBCD(config);

            foreach (var file in fileNames)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return 2;
                }

                console.WriteLine($"Processing {file}...");
                if (!File.Exists(file))
                {
                    console.WriteLine($"File: '{file}' not found. Skipping...");
                    continue;
                }
                var db2Name = Path.GetFileNameWithoutExtension(file);
                var tableName = string.Format(config.SqlTableFormatter ?? "{0}", db2Name.ToLower());
                if (!existingTables.Contains(tableName))
                {
                    console.WriteLine($"Table: '{tableName}' not found. Skipping...");
                    continue;
                }

                var storage = dbcd.Load(db2Name);

                using var progressReporter = new ConsoleProgressReporter(console);
                handler.Handle(new ImportFromMysqlDatabaseOperation()
                {
                    Storage = storage,
                    ProgressReporter = progressReporter,
                    TableName = tableName,
                    DatabaseHost = config.MySqlDatabaseHost,
                    DatabaseName = config.MySqlDatabaseName,
                    DatabasePassword = config.MySqlDatabasePassword ?? string.Empty,
                    DatabasePort = config.MySqlDatabasePort,
                    DatabaseUser = config.MySqlDatabaseUser,
                }, cancellationToken).Wait(cancellationToken);

                storage.Save(Path.Join(outputPath, db2Name + ".db2"));
            }
            return 0;
        }

        int HandleSqliteImport(IConsole console, ImportExportConfig config, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(config.SqliteDatabaseFile))
            {
                console.WriteLine($"No MySql database host provided. This option is required when using output mode {nameof(Mode.Mysql)}");
                return 1;
            }


            var outputPath = config.OutputPath ?? "";
            if (!Directory.Exists(outputPath))
            {
                Directory.CreateDirectory(outputPath);
            }

            console.WriteLine($"Reading existing tables...");
            List<string> existingTables = new();
            try
            {
                var connectionBuilder = new SqliteConnectionStringBuilder
                {
                    DataSource = config.SqliteDatabaseFile,
                };
                var sqlConnection = new SqliteConnection(connectionBuilder.ConnectionString);
                var command = sqlConnection.CreateCommand();
                command.CommandText = "SELECT name FROM sqlite_master WHERE type='table';";
                sqlConnection.Open();
                var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    existingTables.Add(reader.GetString(0));
                }
                sqlConnection.Close();
            }
            catch (SqliteException e)
            {
                if (e.SqliteErrorCode == 26)
                {
                    console.WriteLine($"File: '{config.SqliteDatabaseFile}' was not a valid SQLite database file.");
                    return -1;
                }
                else
                {
                    throw;
                }
            }

            var handler = new ImportFromSqlOperationsHandler();
            var fileNames = GetFileNames(config);
            var dbcd = GetDBCD(config);

            foreach (var file in fileNames)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return 2;
                }

                console.WriteLine($"Processing {file}...");
                if (!File.Exists(file))
                {
                    console.WriteLine($"File: '{file}' not found. Skipping...");
                    continue;
                }
                var db2Name = Path.GetFileNameWithoutExtension(file);
                var tableName = string.Format(config.SqlTableFormatter ?? "{0}", db2Name.ToLower());
                if (!existingTables.Contains(tableName))
                {
                    console.WriteLine($"Table: '{tableName}' not found. Skipping...");
                    continue;
                }

                var storage = dbcd.Load(db2Name);

                using var progressReporter = new ConsoleProgressReporter(console);
                handler.Handle(new ImportFromSQliteDatabaseOperation()
                {
                    Storage = storage,
                    ProgressReporter = progressReporter,
                    TableName = tableName,
                    FileName = config.SqliteDatabaseFile
                }, cancellationToken).Wait(cancellationToken);

                storage.Save(Path.Join(outputPath, db2Name + ".db2"));
            }
            return 0;
        }

        string[] GetFileNames(ImportExportConfig config)
        {
            var fileNames = config.Tables;
            if (fileNames.Length == 0)
            {
                fileNames = Directory.GetFiles(config.InputPath)
                    .Where(x => x.EndsWith(".db2"))
                    .ToArray();
            }
            else
            {
                fileNames = fileNames.Select(x => Path.Join(config.InputPath, x.EndsWith(".db2") ? x : x + ".db2")).ToArray();
            }
            return fileNames;
        }

        DBCD.DBCD GetDBCD(ImportExportConfig config)
        {
            var dbdProvider = new GithubDBDProvider();
            var dbcProvider = new FilesystemDBCProvider(config.InputPath);
            return new DBCD.DBCD(dbcProvider, dbdProvider);
        }

        void HandleConfigInit()
        {
            var outFile = GetConfigFilePath();
            var outDir = Path.GetDirectoryName(outFile);
            var config = new ImportExportConfig();

            var outputJson = JsonSerializer.Serialize(config, options: new JsonSerializerOptions()
            {
                WriteIndented = true,
            });
            
            if (!Directory.Exists(outDir))
            {
                Directory.CreateDirectory(outFile);
            }
            File.WriteAllText(outFile, outputJson);
            Process.Start(new ProcessStartInfo(outFile) { 
                UseShellExecute = true,
            });
        }

        void HandleConfigEdit(InvocationContext context)
        {
            var configFile = GetConfigFilePath();
            if (!File.Exists(configFile))
            {
                context.Console.WriteLine("No config file found. Create one by using the 'config init' command.");
                context.ExitCode = 1;
            }
            Process.Start(new ProcessStartInfo(configFile)
            {
                UseShellExecute = true,
            });
            context.ExitCode = 0;
        }

        static string GetConfigFilePath()
        {
            return Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Constants.AppIdentifier, "exportConfig.json");
        }
    }
}
