using DBCD;
using DBCD.IO.Attributes;
using Google.Protobuf.WellKnownTypes;
using MediatR;
using Microsoft.Data.Sqlite;
using MySql.Data.MySqlClient;
using Org.BouncyCastle.Asn1.Ocsp;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace WDBXEditor2.Core.Operations
{
    public class SQLImportOperationBase : ProgressReportingRequest
    {
        public IDBCDStorage? Storage { get; set; }
        public string TableName { get; set; } = string.Empty;
    }

    public class ImportFromMysqlDatabaseOperation : SQLImportOperationBase
    {
        public string DatabaseHost { get; set; } = string.Empty;
        public uint DatabasePort { get; set; } = 3306;
        public string DatabaseName { get; set; } = string.Empty;
        public string DatabaseUser { get; set; } = string.Empty;
        public string DatabasePassword { get; set; } = string.Empty;
    }

    public class ImportFromSQliteDatabaseOperation : SQLImportOperationBase
    {
        public string FileName { get; set; } = string.Empty;
    }

    public class ImportFromSqlOperationsHandler : IRequestHandler<ImportFromMysqlDatabaseOperation>, IRequestHandler<ImportFromSQliteDatabaseOperation>
    {
        public Task Handle(ImportFromMysqlDatabaseOperation request, CancellationToken cancellationToken)
        {
            var dbcdStorage = request.Storage ?? throw new InvalidOperationException("No DBCD Storage provided for import operation.");

            var stopWatch = new Stopwatch();
            stopWatch.Start();

            dbcdStorage.Clear();

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
                ImportRowsFromSqlConnection(cancellationToken, request, connection, "MySQL", EscapeMySqlName);
                connection.Close();
            }

            stopWatch.Stop();
            Console.WriteLine($"Importing from MySQL: {stopWatch.Elapsed}");
            return Task.CompletedTask;
        }

        public Task Handle(ImportFromSQliteDatabaseOperation request, CancellationToken cancellationToken)
        {
            var dbcdStorage = request.Storage ?? throw new InvalidOperationException("No DBCD Storage provided for import operation.");

            var stopWatch = new Stopwatch();
            stopWatch.Start();

            dbcdStorage.Clear();

            var connectionBuilder = new SqliteConnectionStringBuilder()
            {
                DataSource = request.FileName
            };

            using (var connection = new SqliteConnection(connectionBuilder.ConnectionString))
            {
                connection.Open();
                ImportRowsFromSqlConnection(cancellationToken, request, connection, "SQLite", EscapeSqlName);
                connection.Close();
            }

            stopWatch.Stop();
            Console.WriteLine($"Importing from SQLite: {stopWatch.Elapsed}");
            return Task.CompletedTask;
        }

        private void ImportRowsFromSqlConnection(CancellationToken cancellationToken, SQLImportOperationBase request, DbConnection connection, 
            string dbTypeName, Func<string, string> escapeNameFn)
        {
            var dbcdStorage = request.Storage!;

            var colNames = DBCDHelper.GetColumnNames(dbcdStorage);
            var underlyingType = DBCDHelper.GetUnderlyingType(dbcdStorage);
            var fields = underlyingType.GetFields();

            int idFieldIndex = 0;
            for (var i = 0; i < colNames.Length; i++)
            {
                var fieldName = DBCDHelper.GetUnderlyingFieldName(underlyingType, colNames[i], out var _);
                if (underlyingType.GetField(fieldName)!.GetCustomAttribute<IndexAttribute>() != null)
                {
                    idFieldIndex = i;
                    break;
                }
            }

            var idField = fields.Where(x => x.GetCustomAttribute<IndexAttribute>() != null).FirstOrDefault();
            var processedCount = 0;

            request.ProgressReporter?.SetOperationName($"Import from {dbTypeName} - Checking # of rows to import...");
            var countCommand = connection.CreateCommand();
            countCommand.CommandText = $"SELECT COUNT(*) FROM {escapeNameFn(request.TableName)}";
            var totalRecords = Convert.ToInt64(countCommand.ExecuteScalar());

            request.ProgressReporter?.SetOperationName($"Import from {dbTypeName} - Importing data...");
            var readCommand = connection.CreateCommand();
            readCommand.CommandText = $"SELECT {string.Join(", ", colNames.Select(escapeNameFn))} FROM {escapeNameFn(request.TableName)}";
            var reader = readCommand.ExecuteReader();
            while (reader.Read())
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                var currentCol = 0;

                var id = reader.GetInt32(idFieldIndex);
                var row = dbcdStorage.ConstructRow(id);

                foreach (var field in fields)
                {
                    if (field.GetCustomAttribute<IndexAttribute>() != null)
                    {
                        row[field.Name] = id;
                        currentCol++;
                        continue;
                    }
                    if (field.FieldType.IsArray)
                    {
                        var arrSize = field.GetCustomAttribute<CardinalityAttribute>()!.Count;
                        var arrayRecords = new string[arrSize];

                        for (var i = 0; i < arrSize; i++)
                        {
                            arrayRecords[i] = reader.GetValue(currentCol++).ToString()!;
                        }

                        row[field.Name] = DBCDHelper.ConvertArray(field.FieldType, arrSize, arrayRecords);
                    }
                    else
                    {
                        row[field.Name] = Convert.ChangeType(reader.GetValue(currentCol++), field.FieldType);
                    }
                }
                dbcdStorage.Add(id, row);

                processedCount++;
                var progress = (int)Math.Floor((float)processedCount / totalRecords * 100);
                request.ProgressReporter?.ReportProgress(progress);
            }
        }

        private string EscapeSqlName(string name)
        {
            return "\"" + name + "\"";
        }

        private string EscapeMySqlName(string name)
        {
            return $"`{name}`";
        }

    }
}
