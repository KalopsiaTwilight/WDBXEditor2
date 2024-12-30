using DBCD;
using MediatR;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WDBXEditor2.Core;

namespace WDBXEditor2.Operations
{
    public class ReloadDataViewOperation : ProgressReportingRequest
    {
    }

    public class ReloadDataViewOperationHandler : IRequestHandler<ReloadDataViewOperation>
    {
        private readonly MainWindow _mainWindow;
        public ReloadDataViewOperationHandler(MainWindow window)
        {
            _mainWindow = window;
        }

        public Task Handle(ReloadDataViewOperation request, CancellationToken cancellationToken)
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            var data = new DataTable();
            PopulateColumns(_mainWindow.OpenedDB2Storage, ref data);
            if (_mainWindow.OpenedDB2Storage.Values.Count > 0)
                PopulateDataView(_mainWindow.OpenedDB2Storage, ref data, request.ProgressReporter);

            stopWatch.Stop();
            Console.WriteLine($"Populating grid elapsed time: {stopWatch.Elapsed}");
            data.RowDeleting += _mainWindow.Data_RowDeleted;

            _mainWindow.Dispatcher.Invoke(() =>
            {
                _mainWindow.DB2DataGrid.ItemsSource = data.DefaultView;
            });

            return Task.CompletedTask;
        }


        /// <summary>
        /// Populate the DataView with the DB2 Columns.
        /// </summary>
        private void PopulateColumns(IDBCDStorage storage, ref DataTable data)
        {
            var columnNames = DBCDHelper.GetColumnNames(storage);
            foreach (string columnName in columnNames)
            {
                data.Columns.Add(columnName);
            }
        }

        /// <summary>
        /// Populate the DataView with the DB2 Data.
        /// </summary>
        private void PopulateDataView(IDBCDStorage storage, ref DataTable data, IProgressReporter? progressReporter)
        {
            var rowsProcessed = 0;
            foreach (var rowData in storage.Values)
            {
                var row = data.NewRow();

                foreach (string columnName in rowData.GetDynamicMemberNames())
                {
                    var columnValue = rowData[columnName];

                    if (columnValue.GetType().IsArray)
                    {
                        Array columnValueArray = (Array)columnValue;
                        for (var i = 0; i < columnValueArray.Length; ++i)
                            row[columnName + i] = columnValueArray.GetValue(i);
                    }
                    else
                        row[columnName] = columnValue;
                }

                data.Rows.Add(row);
                rowsProcessed++;

                var progress = (int)((float)rowsProcessed / storage.Values.Count * 100f);
                progressReporter?.ReportProgress(progress);
            }
        }
    }
}
