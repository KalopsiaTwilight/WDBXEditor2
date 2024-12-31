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
        const int InsertsPerStatement = 1000;

        public Task Handle(ExportToSqlFileOperation request, CancellationToken cancellationToken)
        {
            var dbcdStorage = request.Storage ?? throw new InvalidOperationException("No DBCD Storage provided for import operation.");

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
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            using var writer = new StringWriter();
            WriteSqlFile(cancellationToken, request, writer, (x) => $"`{x}`");

            var connectionBuilder = new MySqlConnectionStringBuilder();
            connectionBuilder.Database = request.DatabaseName;
            connectionBuilder.UserID = request.DatabaseUser;
            connectionBuilder.Password = request.DatabasePassword;
            connectionBuilder.Server = request.DatabaseHost;
            connectionBuilder.Port = request.DatabasePort;

            using (var connection = new MySqlConnection(connectionBuilder.GetConnectionString(true)))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = writer.ToString();
                    command.ExecuteNonQuery();
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
                WriteData(cancellationToken, request, writer, escapeFn);
            }
        }

        private void WriteTableDefinition(CancellationToken cancellationToken, IDBCDStorage storage, TextWriter writer, string tableName, Func<string, string> escapeFn)
        {
            var underlyingType = storage.GetType().GetGenericArguments()[0];
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

        private void WriteData(CancellationToken cancellationToken, SQLExportOperationBase request, TextWriter writer, Func<string, string> escapeFn, string operation = "INSERT")
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

                if (processedCount % InsertsPerStatement == 0)
                {
                    if (processedCount != 0)
                    {
                        writer.WriteLine(";");
                        writer.WriteLine();
                    }
                    writer.WriteLine($"{operation} INTO {escapeFn(request.TableName)} ({string.Join(", ", colNames.Select(escapeFn))})");
                    writer.WriteLine("VALUES");
                }
                else
                {
                    writer.WriteLine(",");
                }

                writer.Write($"  ({string.Join(",", colNames.Select(x => GetSqlValue(DBCDHelper.GetDBCRowColumn(row, x))))})");


                processedCount++;
                var progress = (int)Math.Floor((float)processedCount / rows.Count * 100);
                request.ProgressReporter?.ReportProgress(progress);
            }
            writer.WriteLine(";");
            writer.WriteLine();
        }

        private string GetSqlValue(object val)
        {
            if (val == null)
            {
                return "null";
            }
            if (val is string txtVal)
            {
                return "'" + txtVal.Replace("'", "''") + "'";
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
