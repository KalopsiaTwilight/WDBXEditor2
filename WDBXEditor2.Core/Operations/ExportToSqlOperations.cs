using CsvHelper;
using DBCD;
using MediatR;
using Microsoft.Data.Sqlite;
using MySql.Data.MySqlClient;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Transactions;

namespace WDBXEditor2.Core.Operations
{
    public class SQLExportOperationBase : ProgressReportingRequest
    {
        public IDBCDStorage? Storage { get; set; }
        public bool DropTable { get; set; }
        public bool CreateTable { get; set; }
        public bool ExportData { get; set; }
        public string TableName { get; set; } = string.Empty;
        public uint InsertsPerStatement { get; set; } = 1000;
    }

    public class ExportToSqlFileOperation : SQLExportOperationBase
    {
        public string FileName { get; set; } = string.Empty;
    }

    public class ExportToMysqlDatabaseOperation : SQLExportOperationBase
    {
        public string DatabaseHost { get; set;} = string.Empty;
        public uint DatabasePort { get; set; } = 3306;
        public string DatabaseName { get; set; } = string.Empty;
        public string DatabaseUser { get; set;} = string.Empty;
        public string DatabasePassword { get; set; } = string.Empty;

    }

    public class ExportToSQLiteDatabaseOperation: SQLExportOperationBase
    {
        public string FileName { get; set; } = string.Empty;
    }

    public class ExportToSqlOperationsHandler : IRequestHandler<ExportToSqlFileOperation>, IRequestHandler<ExportToMysqlDatabaseOperation>, 
        IRequestHandler<ExportToSQLiteDatabaseOperation>
    {
        public Task Handle(ExportToSqlFileOperation request, CancellationToken cancellationToken)
        {
            var dbcdStorage = request.Storage ?? throw new InvalidOperationException("No DBCD Storage provided for export operation.");

            var stopWatch = new Stopwatch();
            stopWatch.Start();

            request.ProgressReporter?.SetOperationName("Export to SQL File - Writing data...");

            using (var fileStream = File.Create(request.FileName))
            using (var writer = new StreamWriter(fileStream))
            {
                WriteSqlFile(cancellationToken, request, writer);
            }
            stopWatch.Stop();
            Console.WriteLine($"Exporting SQL File: {stopWatch.Elapsed}");
            return Task.CompletedTask;
        }

        public Task Handle(ExportToMysqlDatabaseOperation request, CancellationToken cancellationToken)
        {
            var dbcdStorage = request.Storage ?? throw new InvalidOperationException("No DBCD Storage provided for export operation.");

            request.ProgressReporter?.SetOperationName("Export to MySQL - Creating Table...");
            request.ProgressReporter?.SetIsIndeterminate(true);

            var stopWatch = new Stopwatch();
            stopWatch.Start();

            using var writer = new StringWriter();
            if (request.DropTable)
            {
                writer.WriteLine($"DROP TABLE IF EXISTS `{request.TableName}`;");
                writer.WriteLine();
            }
            if (request.CreateTable)
            {
                WriteTableDefinition(cancellationToken, request.Storage!, writer, request.TableName, EscapeMySqlTable);
            }


            var connectionBuilder = new MySqlConnectionStringBuilder
            {
                Database = request.DatabaseName,
                UserID = request.DatabaseUser,
                Password = request.DatabasePassword,
                Server = request.DatabaseHost,
                Port = request.DatabasePort
            };

            if (cancellationToken.IsCancellationRequested)
                return Task.CompletedTask;

            using (var connection = new MySqlConnection(connectionBuilder.GetConnectionString(true)))
            {
                connection.Open();
                var transaction = connection.BeginTransaction();
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = writer.ToString();
                    command.ExecuteNonQuery();
                }
                if (request.ExportData)
                {
                    request.ProgressReporter?.SetOperationName("Export to MySql - Exporting data...");
                    request.ProgressReporter?.SetIsIndeterminate(false);
                    WriteDataToSQLConnection(cancellationToken, request, connection, transaction, EscapeMySqlTable, MySqlHelper.EscapeString);
                }
                if (cancellationToken.IsCancellationRequested)
                {
                    transaction.Rollback();
                } else
                {
                    transaction.Commit();
                }
                connection.Close();
            }
            stopWatch.Stop();

            Console.WriteLine($"Exporting to MySQL: {stopWatch.Elapsed}");
            return Task.CompletedTask;
        }

        public Task Handle(ExportToSQLiteDatabaseOperation request, CancellationToken cancellationToken)
        {
            var dbcdStorage = request.Storage ?? throw new InvalidOperationException("No DBCD Storage provided for export operation.");

            request.ProgressReporter?.SetOperationName("Export to SQLite - Creating Table...");
            request.ProgressReporter?.SetIsIndeterminate(true);

            var stopWatch = new Stopwatch();
            stopWatch.Start();

            using var writer = new StringWriter();
            if (request.DropTable)
            {
                writer.WriteLine($"DROP TABLE IF EXISTS {EscapeSqlTable(request.TableName)};");
                writer.WriteLine();
            }
            if (request.CreateTable)
            {
                WriteTableDefinition(cancellationToken, request.Storage!, writer, request.TableName, EscapeSqlTable);
            }


            var connectionBuilder = new SqliteConnectionStringBuilder
            {
                DataSource = request.FileName
            };

            if (cancellationToken.IsCancellationRequested)
                return Task.CompletedTask;

            using (var connection = new SqliteConnection(connectionBuilder.ConnectionString))
            {
                connection.Open();
                var transaction = connection.BeginTransaction();
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = writer.ToString();
                    command.ExecuteNonQuery();
                }
                if (request.ExportData)
                {
                    request.ProgressReporter?.SetOperationName("Export to SQLite - Exporting data...");
                    request.ProgressReporter?.SetIsIndeterminate(false);
                    WriteDataToSQLConnection(cancellationToken, request, connection, transaction, EscapeSqlTable, EscapeSqlStringValue);
                }
                if (cancellationToken.IsCancellationRequested)
                {
                    transaction.Rollback();
                }
                else
                {
                    transaction.Commit();
                }
                connection.Close();
            }
            stopWatch.Stop();

            Console.WriteLine($"Exporting to MySQL: {stopWatch.Elapsed}");
            return Task.CompletedTask;
        }

        private void WriteSqlFile(CancellationToken cancellationToken, SQLExportOperationBase request, TextWriter writer)
        {
            if (request.DropTable)
            {
                writer.WriteLine($"DROP TABLE IF EXISTS {EscapeSqlTable(request.TableName)};");
                writer.WriteLine();
            }
            if (request.CreateTable)
            {
                WriteTableDefinition(cancellationToken, request.Storage!, writer, request.TableName, EscapeSqlTable);
            }
            if (request.ExportData)
            {
                WriteDataToFile(cancellationToken, request, writer, EscapeSqlTable);
            }
        }

        private void WriteTableDefinition(CancellationToken cancellationToken, IDBCDStorage storage, TextWriter writer, string tableName, Func<string, string> tableEscapeFn)
        {
            var underlyingType = DBCDHelper.GetUnderlyingType(storage);
            var columns = DBCDHelper.GetColumnNames(storage);

            writer.WriteLine($"CREATE TABLE {tableEscapeFn(tableName)} (");

            for (var i = 0; i < columns.Length; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                if (i > 0)
                {
                    writer.WriteLine(",");
                }
                writer.Write($"  {tableEscapeFn(columns[i])} {GetSqlDataType(DBCDHelper.GetTypeForColumn(underlyingType, columns[i]))} NOT NULL");
            }

            var idField = DBCDHelper.GetIdFieldName(storage);
            if (idField != null)
            {
                writer.WriteLine(",");
                writer.Write($"CONSTRAINT {tableEscapeFn("PK_" + tableName)} PRIMARY KEY ({idField})");
            }

            writer.WriteLine();
            writer.WriteLine(");");
            writer.WriteLine();
        }

        private void WriteDataToFile(CancellationToken cancellationToken, SQLExportOperationBase request, TextWriter writer, Func<string, string> tableEscapeFn)
        {
            var storage = request.Storage!;

            var rows = storage.Values;
            var processedCount = 0;
            var colNames = DBCDHelper.GetColumnNames(storage);
            var enumerator = rows.GetEnumerator();
            while (enumerator.MoveNext())
            {
                if (cancellationToken.IsCancellationRequested) { 
                    return; 
                }

                var row = enumerator.Current;

                if (processedCount % request.InsertsPerStatement == 0)
                {
                    if (processedCount != 0)
                    {
                        writer.WriteLine(";");
                        writer.WriteLine();
                    }
                    writer.WriteLine($"INSERT INTO {tableEscapeFn(request.TableName)}");
                    writer.WriteLine("VALUES");
                }
                else
                {
                    writer.WriteLine(",");
                }

                var rowData = string.Join(",", 
                    colNames.Select(x => GetSqlValue(DBCDHelper.GetDBCRowColumn(row, x), EscapeSqlStringValue))
                );
                writer.Write($"  ({rowData})");

                processedCount++;
                var progress = (int)Math.Floor((float)processedCount / rows.Count * 100);
                request.ProgressReporter?.ReportProgress(progress);
            }
            writer.WriteLine(";");
            writer.WriteLine();
        }

        private void WriteDataToSQLConnection(CancellationToken cancellationToken, SQLExportOperationBase request, DbConnection connection, DbTransaction transaction, 
            Func<string, string> tableEscapeFn, Func<string, string> stringEscapeFn)
        {
            var storage = request.Storage!;

            var rows = storage.Values;
            var processedCount = 0;
            var colNames = DBCDHelper.GetColumnNames(storage);
            var enumerator = rows.GetEnumerator();

            var commandTextWriter = new StringWriter();

            while (enumerator.MoveNext())
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                var row = enumerator.Current;

                if (processedCount % request.InsertsPerStatement == 0)
                {
                    if (processedCount != 0)
                    {
                        commandTextWriter.WriteLine(";");

                        var command = connection.CreateCommand();
                        command.Transaction = transaction;
                        command.CommandText = commandTextWriter.ToString();
                        command.ExecuteNonQuery();

                        commandTextWriter.GetStringBuilder().Length = 0;
                    }
                    commandTextWriter.WriteLine($"INSERT INTO {tableEscapeFn(request.TableName)} ");
                    commandTextWriter.WriteLine("VALUES");
                }
                else
                {
                    commandTextWriter.WriteLine(",");
                }

                var rowData = string.Join(",",
                    colNames.Select(x => GetSqlValue(DBCDHelper.GetDBCRowColumn(row, x), stringEscapeFn))
                );
                commandTextWriter.Write($"({rowData})");


                processedCount++;
                var progress = (int)Math.Floor((float)processedCount / rows.Count * 100);
                request.ProgressReporter?.ReportProgress(progress);
            }


            commandTextWriter.WriteLine(";");
            commandTextWriter.WriteLine();
            var finalCommand = connection.CreateCommand();
            finalCommand.Transaction = transaction;
            finalCommand.CommandText = commandTextWriter.ToString();
            finalCommand.ExecuteNonQuery();
        }


        private string GetSqlValue(object val, Func<string, string> stringEscapeFn)
        {
            if (val == null)
            {
                return "null";
            }
            if (val is string txtVal)
            {
                return  $"'{stringEscapeFn(txtVal)}'";
            }
            if (val is float floatVal)
            {
                return floatVal.ToString(CultureInfo.InvariantCulture);
            }

            return val.ToString() ?? string.Empty;
        }

        private string GetSqlDataType(Type t)
        {
            switch (t.Name)
            {
                case nameof(UInt64): return "BIGINT UNSIGNED";
                case nameof(Int64): return "BIGINT";
                case nameof(Single): return "FLOAT";
                case nameof(Int32): return "INT";
                case nameof(UInt32): return "INT UNSIGNED";
                case nameof(Int16): return "SMALLINT";
                case nameof(UInt16): return "SMALLINT UNSIGNED";
                case nameof(Byte): return "TINYINT UNSIGNED";
                case nameof(SByte): return "TINYINT";
                case nameof(String): return "TEXT";
            }
            throw new InvalidOperationException("Unknown datatype encounted in SQL conversion: " + t.Name);
        }

        private string EscapeMySqlTable(string table)
        {
            return "`" + table + "`";
        }

        private string EscapeSqlTable(string table)
        {
            return "\"" + table + "\"";
        }

        private string EscapeSqlStringValue(string value)
        {
            return value.Replace("'", "''");
        }
    }
}
