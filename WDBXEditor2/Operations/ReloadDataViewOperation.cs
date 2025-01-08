using DBCD;
using DBCD.IO.Attributes;
using MediatR;
using SQLitePCL;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
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
            collection.CollectionChanged += (s, e) =>
            {
                if (e.Action == NotifyCollectionChangedAction.Remove)
                {
                    foreach(var item in e.OldItems)
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
                        if (item is DBCDRowProxy proxy)
                        {
                            var id = storage.Keys.Count > 0 ? storage.Keys.Max() + 1 : 1;
                            var rowData = storage.ConstructRow(id);
                            rowData[DBCDHelper.GetIdFieldName(storage)] = id;
                            rowData.ID = id;

                            // Resize arrays to their actual read size

                            var firstRow = storage.Values.FirstOrDefault();
                            if (firstRow != null)
                            {
                                var arrayFields = rowData.GetUnderlyingType().GetFields().Where(x => x.FieldType.IsArray);
                                foreach (var arrayField in arrayFields)
                                {
                                    var count = ((Array)firstRow[arrayField.Name]).Length;
                                    Array arrayData = Array.CreateInstance(arrayField.FieldType.GetElementType(), count);
                                    for (var i = 0; i < count; i++)
                                    {
                                        arrayData.SetValue(Activator.CreateInstance(arrayField.FieldType.GetElementType()), i);
                                    }
                                    rowData[arrayField.Name] = arrayData;
                                }
                            }

                            storage[id] = rowData;

                            proxy.RowData = rowData;
                        }
                    }
                }
            };

            _mainWindow.Dispatcher.Invoke(() =>
            {
                _mainWindow.DB2DataGrid.ItemsSource = collection;
            });

            stopWatch.Stop();
            Console.WriteLine($"Populating grid elapsed time: {stopWatch.Elapsed}");
            return Task.CompletedTask;
        }
    }
}
