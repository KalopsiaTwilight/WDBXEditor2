using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper.Configuration;
using CsvHelper;
using MediatR;
using DBCD;

namespace WDBXEditor2.Core.Operations
{
    public class ExportToCsvOperation: ProgressReportingRequest
    {
        public IDBCDStorage? Storage { get; set; }
        public string FileName { get; set; } = string.Empty;
    }

    public class ExportToCsvOperationHandler : IRequestHandler<ExportToCsvOperation>
    {
        public async Task Handle(ExportToCsvOperation request, CancellationToken cancellationToken)
        {
            var dbcdStorage = request.Storage ?? throw new InvalidOperationException("No DBCD Storage provided for export operation.");

            var columnNames = DBCDHelper.GetColumnNames(dbcdStorage);
            using var fileStream = File.Create(request.FileName);
            using var writer = new StreamWriter(fileStream);
            await writer.WriteLineAsync(string.Join(",", columnNames));
            using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                MemberTypes = MemberTypes.Fields,
                HasHeaderRecord = false,
                ShouldQuote = (args) =>
                {
                    return args.FieldType == typeof(string);
                }
            }))
            {
                csv.Context.TypeConverterCache.RemoveConverter<byte[]>();
                var rows = dbcdStorage.Values;
                for (var i = 0; i < rows.Count; i++)
                {
                    var progress = (int) ((float) i / rows.Count * 100f);
                    request.ProgressReporter?.ReportProgress(progress);
                    csv.WriteRecord(rows.ElementAt(i));
                    await csv.NextRecordAsync();
                    //awauwriter.Write(Environment.NewLine);
                }
            }
        }
    }
}
