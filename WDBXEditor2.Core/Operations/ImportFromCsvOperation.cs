using System.Globalization;
using CsvHelper.Configuration;
using CsvHelper;
using MediatR;
using DBCD;
using DBCD.IO.Attributes;
using System.Reflection;

namespace WDBXEditor2.Core.Operations
{
    public class ImportFromCsvOperation: ProgressReportingRequest
    {
        public IDBCDStorage? Storage { get; set; }
        public string FileName { get; set; } = string.Empty;
    }

    public class ImportFromCsvOperationHandler : IRequestHandler<ImportFromCsvOperation>
    {
        public async Task Handle(ImportFromCsvOperation request, CancellationToken cancellationToken)
        {
            var dbcdStorage = request.Storage ?? throw new InvalidOperationException("No DBCD Storage provided for import operation.");

            var totalRecords = File.ReadAllLines(request.FileName).Length - 1;
            var processed = 0f;

            request.ProgressReporter?.SetOperationName("Import from CSV - Importing data...");

            using (var reader = new StreamReader(request.FileName))
            using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                MemberTypes = CsvHelper.Configuration.MemberTypes.Fields,
                HasHeaderRecord = true,

            }))
            {
                var underlyingType = dbcdStorage.GetType().GenericTypeArguments[0]!;
                csv.Context.TypeConverterCache.RemoveConverter<byte[]>();

                await csv.ReadAsync();
                csv.ReadHeader();
                if (csv.HeaderRecord == null)
                {
                    throw new InvalidOperationException("Unable to import CSV file without headers.");
                }

                dbcdStorage.Clear();
                while(await csv.ReadAsync())
                {
                    if (cancellationToken.IsCancellationRequested) 
                        break;

                    var record = csv.GetRecord(underlyingType);

                    var id = (int)underlyingType.GetField(dbcdStorage.AvailableColumns.First())!.GetValue(record)!;
                    var row = dbcdStorage.ConstructRow(id);

                    var fields = underlyingType.GetFields();
                    foreach (var field in fields)
                    {
                        if (field.FieldType.IsArray)
                        {
                            var count = field.GetCustomAttribute<CardinalityAttribute>()!.Count;
                            var rowRecords = new string[count];
                            
                            Array.Copy(csv.Parser.Record!, Array.IndexOf(csv.HeaderRecord, field.Name + 0), rowRecords, 0, count);
                            row[field.Name] = DBCDHelper.ConvertArray(field.FieldType, count, rowRecords);
                        }
                        else
                        {
                            row[field.Name] = field.GetValue(record);
                        }
                    }
                    dbcdStorage.Add(id, row);

                    var progress = (int)(processed / totalRecords * 100f);
                    request.ProgressReporter?.ReportProgress(progress);
                    processed++;
                }
            }
        }
    }
}
