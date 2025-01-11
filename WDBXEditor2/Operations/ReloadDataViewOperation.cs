using DBCD;
using DBCD.IO;
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
                viewSource.Filter += ViewSource_Filter;
                _mainWindow.DB2DataGrid.ItemsSource = viewSource.View;
            });
            UpdateDb2Stats();

            stopWatch.Stop();
            Console.WriteLine($"Populating grid elapsed time: {stopWatch.Elapsed}");
            return Task.CompletedTask;
        }

        private void UpdateDb2Stats()
        {
            _mainWindow.Dispatcher.Invoke(() =>
            {
                var totalCount = _mainWindow.OpenedDB2Storage.Count;
                var colCount = DBCDHelper.GetColumnNames(_mainWindow.OpenedDB2Storage).Length;
                var isFiltered = _mainWindow.Filter.Type != Misc.FilterType.None;
                var filteredCount = _mainWindow.OpenedDB2Storage.Values.Where(MatchesFilter).Count();

                _mainWindow.tbCurrentDb2Stats.Text = $"{totalCount} rows{(isFiltered ? $" ({filteredCount} visible)" : "")}, {colCount} columns";
            });
        }

        private void ViewSource_Filter(object sender, FilterEventArgs e)
        {
            if (e.Item is not DBCDRowProxy proxy)
            {
                return;
            }
            e.Accepted = MatchesFilter(proxy.RowData);
        }

        private bool MatchesFilter(DBCDRow row)
        {
            if (_mainWindow.Filter.Type == Misc.FilterType.None)
            {
                return true;
            }
            var colVal = DBCDHelper.GetDBCRowColumn(row, _mainWindow.Filter.Column).ToString();
            switch (_mainWindow.Filter.Type)
            {
                case Misc.FilterType.Exact: return colVal.Equals(_mainWindow.Filter.Value);
                case Misc.FilterType.Contains: return colVal.Contains(_mainWindow.Filter.Value);
                case Misc.FilterType.RegEx: return _mainWindow.Filter.AsRegex.IsMatch(colVal);
                default: return false;
            }
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
            UpdateDb2Stats();
        }
    }
}
