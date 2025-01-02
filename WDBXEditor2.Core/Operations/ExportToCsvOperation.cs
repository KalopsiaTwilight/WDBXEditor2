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
using System.Diagnostics;

namespace WDBXEditor2.Core.Operations
{
    public class ExportToCsvOperation: ProgressReportingRequest
    {
        public IDBCDStorage? Storage { get; set; }
        public string FileName { get; set; } = string.Empty;
    }

    public class ExportToCsvOperationHandler : IRequestHandler<ExportToCsvOperation>
    {
        public Task Handle(ExportToCsvOperation request, CancellationToken cancellationToken)
        {
            var dbcdStorage = request.Storage ?? throw new InvalidOperationException("No DBCD Storage provided for export operation.");

            var columnNames = DBCDHelper.GetColumnNames(dbcdStorage);
            using var fileStream = File.Create(request.FileName);
            using var writer = new StreamWriter(fileStream);
            writer.WriteLine(string.Join(",", columnNames));

            var stopWatch = new Stopwatch();
            stopWatch.Start();

            request.ProgressReporter?.SetOperationName("Export to CSV - Writing data...");

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
                var enumerator = rows.GetEnumerator();
                enumerator.MoveNext();
                for (var i = 0; i < rows.Count; i++)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    var progress = (int)((float)i / rows.Count * 100f);
                    request.ProgressReporter?.ReportProgress(progress);
                    csv.WriteRecord(enumerator.Current);
                    csv.NextRecord();
                    enumerator.MoveNext();
                }
            }


            stopWatch.Stop();
            Console.WriteLine($"Exporting CSV. Elapsed Time: {stopWatch.Elapsed}");

            return Task.CompletedTask;
        }
    }
}
