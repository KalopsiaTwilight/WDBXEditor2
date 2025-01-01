using DBCD;
using DBCD.IO.Attributes;
using Google.Protobuf.WellKnownTypes;
using MediatR;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
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

    public class ImportFromSqlOperationsHandler : IRequestHandler<ImportFromMysqlDatabaseOperation>
    {
        public Task Handle(ImportFromMysqlDatabaseOperation request, CancellationToken cancellationToken)
        {
            var dbcdStorage = request.Storage ?? throw new InvalidOperationException("No DBCD Storage provided for import operation.");

            var stopWatch = new Stopwatch();
            stopWatch.Start();

            var colNames = DBCDHelper.GetColumnNames(dbcdStorage);

            var connectionBuilder = new MySqlConnectionStringBuilder
            {
                Database = request.DatabaseName,
                UserID = request.DatabaseUser,
                Password = request.DatabasePassword,
                Server = request.DatabaseHost,
                Port = request.DatabasePort
            };

            dbcdStorage.Clear();

            var underlyingType = DBCDHelper.GetUnderlyingType(dbcdStorage);
            var fields = underlyingType.GetFields();
            var processedCount = 0;

            using (var connection = new MySqlConnection(connectionBuilder.GetConnectionString(true)))
            {
                connection.Open();

                var countCommand = connection.CreateCommand(); 
                countCommand.CommandText = $"SELECT COUNT(*) FROM {EscapeMySqlName(request.TableName)}";
                var totalRecords = (long) countCommand.ExecuteScalar();

                var readCommand = connection.CreateCommand();
                readCommand.CommandText = $"SELECT {string.Join(", ", colNames.Select(EscapeMySqlName))} FROM {EscapeMySqlName(request.TableName)}";
                var reader = readCommand.ExecuteReader();
                while (reader.Read())
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return Task.CompletedTask;
                    }
                    var currentCol = 0;
                    // TODO: CHheck if this is always true
                    var id = (int)reader.GetValue(currentCol++);
                    var row = dbcdStorage.ConstructRow(id);

                    foreach (var field in fields)
                    {
                        if (field.GetCustomAttribute<IndexAttribute>() != null)
                        {
                            row[field.Name] = id;
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
                connection.Close();
            }


            stopWatch.Stop();
            Console.WriteLine($"Importing from MySQL: {stopWatch.Elapsed}");
            return Task.CompletedTask;
        }

        private string EscapeMySqlName(string name)
        {
            return $"`{name}`";
        }
    }
}
