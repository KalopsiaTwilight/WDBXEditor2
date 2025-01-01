using CsvHelper;
using DBCD;
using MediatR;
using MySql.Data.MySqlClient;
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

    public class ExportToSqlOperationsHandler : IRequestHandler<ExportToSqlFileOperation>, IRequestHandler<ExportToMysqlDatabaseOperation>
    {
        public Task Handle(ExportToSqlFileOperation request, CancellationToken cancellationToken)
        {
            var dbcdStorage = request.Storage ?? throw new InvalidOperationException("No DBCD Storage provided for export operation.");

            var stopWatch = new Stopwatch();
            stopWatch.Start();

            using (var fileStream = File.Create(request.FileName))
            using (var writer = new StreamWriter(fileStream))
            {
                WriteSqlFile(cancellationToken, request, writer, (x) => $"\"{x}\"");
            }
            stopWatch.Stop();
            Console.WriteLine($"Exporting SQL File: {stopWatch.Elapsed}");
            return Task.CompletedTask;
        }

        public Task Handle(ExportToMysqlDatabaseOperation request, CancellationToken cancellationToken)
        {
            var dbcdStorage = request.Storage ?? throw new InvalidOperationException("No DBCD Storage provided for export operation.");

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
                WriteTableDefinition(cancellationToken, request.Storage!, writer, request.TableName, (x) => $"`{x}`");
            }

            var connectionBuilder = new MySqlConnectionStringBuilder
            {
                Database = request.DatabaseName,
                UserID = request.DatabaseUser,
                Password = request.DatabasePassword,
                Server = request.DatabaseHost,
                Port = request.DatabasePort
            };

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
                    WriteDataToMySQL(cancellationToken, request, connection, transaction);
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


        private void WriteSqlFile(CancellationToken cancellationToken, SQLExportOperationBase request, TextWriter writer, Func<string, string> escapeFn)
        {
            if (request.DropTable)
            {
                writer.WriteLine($"DROP TABLE IF EXISTS {escapeFn(request.TableName)};");
                writer.WriteLine();
            }
            if (request.CreateTable)
            {
                WriteTableDefinition(cancellationToken, request.Storage!, writer, request.TableName, escapeFn);
            }
            if (request.ExportData)
            {
                WriteDataToFile(cancellationToken, request, writer, escapeFn);
            }
        }

        private void WriteTableDefinition(CancellationToken cancellationToken, IDBCDStorage storage, TextWriter writer, string tableName, Func<string, string> escapeFn)
        {
            var underlyingType = DBCDHelper.GetUnderlyingType(storage);
            var columns = DBCDHelper.GetColumnNames(storage);

            writer.WriteLine($"CREATE TABLE {escapeFn(tableName)} (");

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
                writer.Write($"  {escapeFn(columns[i])} {GetSqlDataType(DBCDHelper.GetTypeForColumn(underlyingType, columns[i]))}");
            }

            writer.WriteLine();
            writer.WriteLine(");");
            writer.WriteLine();
        }

        private void WriteDataToFile(CancellationToken cancellationToken, SQLExportOperationBase request, TextWriter writer, Func<string, string> escapeFn)
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
                    writer.WriteLine($"INSERT INTO {escapeFn(request.TableName)}");
                    writer.WriteLine("VALUES");
                }
                else
                {
                    writer.WriteLine(",");
                }

                var rowData = string.Join(",", 
                    colNames.Select(x => GetSqlValue(DBCDHelper.GetDBCRowColumn(row, x), (txtVal) => txtVal.Replace("'", "''")))
                );
                writer.Write($"  ({rowData})");

                processedCount++;
                var progress = (int)Math.Floor((float)processedCount / rows.Count * 100);
                request.ProgressReporter?.ReportProgress(progress);
            }
            writer.WriteLine(";");
            writer.WriteLine();
        }

        private void WriteDataToMySQL(CancellationToken cancellationToken, SQLExportOperationBase request, MySqlConnection connection, MySqlTransaction transaction)
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
                    commandTextWriter.WriteLine($"INSERT INTO `{request.TableName}`");
                    commandTextWriter.WriteLine("VALUES");
                }
                else
                {
                    commandTextWriter.WriteLine(",");
                }

                var rowData = string.Join(",",
                    colNames.Select(x => GetSqlValue(DBCDHelper.GetDBCRowColumn(row, x), MySqlHelper.EscapeString))
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

        private string GetSqlValue(object val, Func<string, string> escapeStringFn)
        {
            if (val == null)
            {
                return "null";
            }
            if (val is string txtVal)
            {
                return  $"'{escapeStringFn(txtVal)}'";
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
    }
}
