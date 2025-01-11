using DBCD;
using MediatR;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
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
            request.ProgressReporter?.SetOperationName("Preparing dataview...");
            request.ProgressReporter?.SetIsIndeterminate(true);

            var stopWatch = new Stopwatch();
            stopWatch.Start();

            var storage = _mainWindow.OpenedDB2Storage;

            // Set type descriptor for dbcd types to current storage to provide type metadata to DataGrid
            var typeDescProvider = new DBCDRowTypeDescriptionProvider(storage);

            TypeDescriptor.RemoveProvider(TypeDescriptor.GetProvider(typeof(DBCDRow)), typeof(DBCDRow));
            TypeDescriptor.AddProvider(typeDescProvider, typeof(DBCDRow));

            TypeDescriptor.RemoveProvider(TypeDescriptor.GetProvider(typeof(DBCDRowProxy)), typeof(DBCDRowProxy));
            TypeDescriptor.AddProvider(typeDescProvider, typeof(DBCDRowProxy));

            // Create observable collection for datagrid view
            var collection = new ObservableCollection<DBCDRowProxy>();  
            foreach (var row in storage.Values)
            {
                collection.Add(new DBCDRowProxy(row));
            }
            collection.CollectionChanged += OnCollectionChanged;
            _mainWindow.DataGridSource = collection;


            _mainWindow.Dispatcher.Invoke(() =>
            {
                var viewSource = new CollectionViewSource
                {
                    Source = collection
                };
                //viewSource.Filter += ViewSource_Filter;
                _mainWindow.DB2DataGrid.ItemsSource = viewSource.View;
            });

            stopWatch.Stop();
            Console.WriteLine($"Populating grid elapsed time: {stopWatch.Elapsed}");
            return Task.CompletedTask;
        }

        private void ViewSource_Filter(object sender, FilterEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            var storage = _mainWindow.OpenedDB2Storage;
            if (e.Action == NotifyCollectionChangedAction.Remove)
            {
                foreach (var item in e.OldItems)
                {
                    if (item is DBCDRowProxy proxy)
                    {
                        storage.Remove(proxy.RowData.ID);
                    }
                }
            }
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (var item in e.NewItems)
                {
                    if (item is DBCDRowProxy proxy && proxy.RowData == null)
                    {
                        var rowData = DBCDHelper.ConstructNewRow(storage);
                        storage[rowData.ID] = rowData;
                        proxy.RowData = rowData;
                    }
                }
            }
        }
    }
}
