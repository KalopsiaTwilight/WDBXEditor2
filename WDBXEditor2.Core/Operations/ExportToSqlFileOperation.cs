using CsvHelper;
using DBCD;
using MediatR;
using System.Globalization;

namespace WDBXEditor2.Core.Operations
{
    public class ExportToSqlFileOperation : ProgressReportingRequest
    {
        public IDBCDStorage? Storage { get; set; }
        public string FileName { get; set; } = string.Empty;
        public bool DropTable { get; set; }
        public bool CreateTable { get; set; }
        public bool ExportData { get; set; }
        public string TableName { get; set; } = string.Empty;
    }

    public class ExportToSqlFileOperationHandler : IRequestHandler<ExportToSqlFileOperation>
    {
        const int InsertsPerStatement = 1000;

        public async Task Handle(ExportToSqlFileOperation request, CancellationToken cancellationToken)
        {
            var dbcdStorage = request.Storage ?? throw new InvalidOperationException("No DBCD Storage provided for import operation.");

            using (var fileStream = File.Create(request.FileName))
            using (var writer = new StreamWriter(fileStream))
            {
                if (request.DropTable)
                {
                    await writer.WriteLineAsync($"DROP TABLE IF EXISTS {request.TableName};");
                    await writer.WriteLineAsync();
                }
                if (request.CreateTable)
                {
                    await WriteTableDefinition(dbcdStorage, writer, request.TableName);
                }
                if (request.ExportData)
                {
                    await WriteData(cancellationToken, request, writer);
                }
            }
        }

        private async Task WriteTableDefinition(IDBCDStorage storage, TextWriter writer, string tableName)
        {
            var underlyingType = storage.GetType().GetGenericArguments()[0];
            var columns = DBCDHelper.GetColumnNames(storage);

            await writer.WriteLineAsync($"CREATE TABLE {tableName} (");

            for (var i = 0; i < columns.Length; i++)
            {
                if (i > 0)
                {
                    await writer.WriteLineAsync(",");
                }
                await writer.WriteAsync($"  {columns[i]} {GetSqlDataType(DBCDHelper.GetTypeForColumn(underlyingType, columns[i]))}");
            }

            await writer.WriteLineAsync();
            await writer.WriteLineAsync(");");
            await writer.WriteLineAsync();
        }

        private async Task WriteData(CancellationToken cancellationToken, ExportToSqlFileOperation request, TextWriter writer, string operation = "INSERT")
        {
            var storage = request.Storage!;

            var rows = storage.Values;
            var processedCount = 0;
            var colNames = DBCDHelper.GetColumnNames(storage);

            while (processedCount < rows.Count)
            {
                if (cancellationToken.IsCancellationRequested) { 
                    return; 
                }

                var row = rows.ElementAt(processedCount);

                if (processedCount % InsertsPerStatement == 0)
                {
                    if (processedCount != 0)
                    {
                        await writer.WriteLineAsync(";");
                        await writer.WriteLineAsync();
                    }
                    await writer.WriteLineAsync($"{operation} INTO {request.TableName} ({string.Join(", ", colNames)})");
                    await writer.WriteLineAsync("VALUES");
                }
                else
                {
                    await writer.WriteLineAsync(",");
                }

                await writer.WriteAsync($"  ({string.Join(",", colNames.Select(x => GetSqlValue(DBCDHelper.GetDBCRowColumn(row, x))))})");


                processedCount++;
                var progress = (int)Math.Floor((float)processedCount / rows.Count * 100);
                request.ProgressReporter?.ReportProgress(progress);
            }
            await writer.WriteLineAsync(";");
            await writer.WriteLineAsync();
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
