﻿using DBCD;
using MediatR;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WDBXEditor2.Core.Operations
{
    public class SaveDb2ToFileOperation: ProgressReportingRequest
    {
        public IDBCDStorage? Storage { get; set; }
        public string FileName { get; set; } = string.Empty;
    }

    public class SaveDb2ToFileOperationHandler : IRequestHandler<SaveDb2ToFileOperation>
    {
        public Task Handle(SaveDb2ToFileOperation request, CancellationToken cancellationToken)
        {
            var dbcdStorage = request.Storage ?? throw new InvalidOperationException("No DBCD Storage provided for export operation.");

            var stopWatch = new Stopwatch();
            stopWatch.Start();

            request.ProgressReporter?.SetOperationName("Save DB2 - Writing data...");
            request.ProgressReporter?.SetIsIndeterminate(true);

            // Fix changed ID fields
            var rowsToReadd = dbcdStorage.ToDictionary().Where(pair => pair.Key != pair.Value.ID).ToList();
            foreach(var row in rowsToReadd)
            {
                if (dbcdStorage.ContainsKey(row.Value.ID))
                {
                    throw new InvalidOperationException($"Unable to save DB2 file due to duplicated row ids. Row id: '{row.Value.ID}' exists one more or times.");
                }
                dbcdStorage.Remove(row.Key);
                dbcdStorage.Add(row.Value.ID, row.Value);    
            }

            var tempFile = Path.GetTempFileName();
            dbcdStorage.Save(tempFile);

            File.Move(tempFile, request.FileName, true);

            stopWatch.Stop();
            Console.WriteLine($"Saving DB2. Elapsed Time: {stopWatch.Elapsed}");

            return Task.CompletedTask;
        }
    }
}
