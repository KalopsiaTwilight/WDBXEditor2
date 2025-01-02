using DBCD;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WDBXEditor2.Controller;
using WDBXEditor2.Core.Operations;
using WDBXEditor2.Views;
using WDBXEditor2.Core;
using WDBXEditor2.Operations;
using System.Threading;
using System.Threading.Tasks;

namespace WDBXEditor2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private DBLoader dbLoader;
        public string CurrentOpenDB2 { get; set; } = string.Empty;

        public Dictionary<string, string> OpenedDB2Paths { get; set; } = new Dictionary<string, string>();
        public IDBCDStorage OpenedDB2Storage { get; set; }

        private List<DBCDRow> _currentOrderedRows = new();

        private readonly IServiceProvider _serviceProvider;
        private IMediator _mediator;
        private IProgressReporter _progressReporter;

        public MainWindow(IServiceProvider serviceProvider)
        {
            InitializeComponent();
            _serviceProvider = serviceProvider;
            _progressReporter = _serviceProvider.GetService<IProgressReporter>();
            _mediator = _serviceProvider.GetService<IMediator>();
            dbLoader = ActivatorUtilities.CreateInstance<DBLoader>(_serviceProvider);


            Exit.Click += (e, o) => Close();

            Title = $"WDBXEditor2  -  {Constants.Version}";
        }

        public override void BeginInit()
        {
            base.BeginInit();
        }


        private void Open_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "DB2 Files (*.db2)|*.db2",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer)
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var files = openFileDialog.FileNames;

                DefinitionSelect definitionSelect = ActivatorUtilities.CreateInstance<DefinitionSelect>(_serviceProvider);
                definitionSelect.SetDefinitionFromVersionDefinitions(dbLoader.GetVersionDefinitionsForDB2(dbLoader.GetDb2Name(files[0])));
                definitionSelect.ShowDialog();

                if (definitionSelect.IsCanceled)
                {
                    return;
                }

                Locale selectedLocale = definitionSelect.SelectedLocale;
                string build = definitionSelect.SelectedVersion;
                txtOperation.Text = "Parsing DB2 files...";
                ProgressBar.IsIndeterminate = true;

                Task.Run(() =>
                {
                    var loadedDBs = dbLoader.LoadFiles(files, build, selectedLocale);

                    Dispatcher.Invoke(() =>
                    {
                        foreach (string loadedDB in loadedDBs)
                        {
                            OpenedDB2Paths[loadedDB] = files.First(x => Path.GetFileNameWithoutExtension(x) == loadedDB);
                            OpenDBItems.Items.Add(loadedDB);
                        }

                        ProgressBar.IsIndeterminate = false;
                        txtOperation.Text = "";
                    });
                });

            }
        }

        private void OpenDBItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Clear DataGrid
            DB2DataGrid.Columns.Clear();
            DB2DataGrid.ItemsSource = Array.Empty<int>();

            CurrentOpenDB2 = (string)OpenDBItems.SelectedItem;
            if (CurrentOpenDB2 == null)
                return;

            if (dbLoader.LoadedDBFiles.TryGetValue(CurrentOpenDB2, out IDBCDStorage storage))
            {
                Title = $"WDBXEditor2  -  {Constants.Version}  -  {CurrentOpenDB2}";
                OpenedDB2Storage = storage;
                _currentOrderedRows = storage.ToDictionary().OrderBy(x => x.Key).Select(x => x.Value).ToList();
                ReloadDataView();
            }
        }

        /// <summary>
        /// Close the currently opened DB2 file.
        /// </summary>
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Title = $"WDBXEditor2  -  {Constants.Version}";

            // Remove the DB2 file from the open files.
            OpenDBItems.Items.Remove(CurrentOpenDB2);

            // Clear DataGrid
            DB2DataGrid.Columns.Clear();
            DB2DataGrid.ItemsSource = Array.Empty<int>();

            CurrentOpenDB2 = string.Empty;
            OpenedDB2Storage = null;
            _currentOrderedRows = null;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(CurrentOpenDB2))
            {
                dbLoader.LoadedDBFiles[CurrentOpenDB2].Save(OpenedDB2Paths[CurrentOpenDB2]);
                dbLoader.ReloadFile(OpenedDB2Paths[CurrentOpenDB2]);
                ReloadDataView();
            }
        }

        private void SaveAs_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(CurrentOpenDB2))
                return;

            var saveFileDialog = new SaveFileDialog
            {
                FileName = CurrentOpenDB2,
                Filter = "DB2 Files (*.db2)|*.db2",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer)
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                dbLoader.LoadedDBFiles[CurrentOpenDB2].Save(saveFileDialog.FileName);
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void DB2DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit)
            {
                if (e.Column != null)
                {
                    var rowIdx = e.Row.GetIndex();
                    var newVal = e.EditingElement as TextBox;

                    var dbcRow = _currentOrderedRows.ElementAt(rowIdx);
                    var colName = e.Column.Header.ToString();
                    try
                    {
                        DBCDHelper.SetDBCRowColumn(dbcRow, colName, newVal.Text);
                        if (colName == dbcRow.GetDynamicMemberNames().FirstOrDefault())
                        {
                            OpenedDB2Storage.Remove(dbcRow.ID);
                            dbcRow.ID = Convert.ToInt32(dbcRow[colName]);
                            OpenedDB2Storage.Add(dbcRow.ID, dbcRow);
                        }
                    }
                    catch (Exception exc)
                    {
                        newVal.Text = DBCDHelper.GetDBCRowColumn(dbcRow, colName).ToString();
                        var exceptionWindow = new ExceptionWindow();
                        var fieldType = DBCDHelper.GetTypeForColumn(dbcRow, colName);

                        exceptionWindow.DisplayException(exc.InnerException ?? exc, $"An error occured setting this value for this cell. This is likely due to an invalid value for conversion to '{fieldType.Name}':");
                        exceptionWindow.Show();
                    }

                    Console.WriteLine($"RowIdx: {rowIdx} Text: {newVal.Text}");
                }
            }
        }

        private void ExportCsv_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(CurrentOpenDB2))
                return;

            var saveFileDialog = new SaveFileDialog
            {
                FileName = Path.GetFileNameWithoutExtension(CurrentOpenDB2) + ".csv",
                Filter = "Comma Seperated Values Files (*.csv)|*.csv",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer)
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                RunOperationAsync(new ExportToCsvOperation()
                {
                    FileName = saveFileDialog.FileName,
                    Storage = OpenedDB2Storage,
                });
            }
        }

        private void ImportCsv_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(CurrentOpenDB2))
                return;

            var openFileDialog = new OpenFileDialog
            {
                Multiselect = false,
                Filter = "Comma Seperated Values Files (*.csv)|*.csv",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer)
            };

            if (openFileDialog.ShowDialog() == true)
            {
                var fileName = openFileDialog.FileNames[0];
                RunOperationAsync(new ImportFromCsvOperation()
                {
                    FileName = fileName,
                    Storage = OpenedDB2Storage
                }, true);
            }
        }

        private void ExportSql_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(CurrentOpenDB2))
                return;

            ActivatorUtilities.CreateInstance<ExportSqlWindow>(_serviceProvider).Show();
        }

        private void ImportSql_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(CurrentOpenDB2))
                return;

            ActivatorUtilities.CreateInstance<ImportSqlWindow>(_serviceProvider).Show();
        }

        private void SetColumn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(CurrentOpenDB2))
                return;
            new SetColumnWindow(this).Show();
        }

        private void ReplaceColumn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(CurrentOpenDB2))
                return;

            new ReplaceColumnWindow(this).Show();
        }

        private void SetBitColumn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(CurrentOpenDB2))
                return;

            new SetFlagWindow(this).Show();
        }

        private void SetDependentColumn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(CurrentOpenDB2))
                return;

            new SetDependentColumnWindow(this).Show();
        }

        internal void Data_RowDeleted(object sender, DataRowChangeEventArgs e)
        {
            var rowId = int.Parse(e.Row[0].ToString());
            _currentOrderedRows.Remove(OpenedDB2Storage[rowId]);
            OpenedDB2Storage.Remove(rowId);
        }

        private void DB2DataGrid_InitializingNewItem(object sender, InitializingNewItemEventArgs e)
        {
            Debug.WriteLine(e.NewItem);

            var id = OpenedDB2Storage.Keys.Count > 0 ? OpenedDB2Storage.Keys.Max() + 1 : 1;
            var rowData = OpenedDB2Storage.ConstructRow(id);
            rowData[rowData.GetDynamicMemberNames().First()] = id;
            rowData.ID = id;

            OpenedDB2Storage[id] = rowData;
            _currentOrderedRows.Insert(_currentOrderedRows.Count, rowData);

            foreach (string columnName in rowData.GetDynamicMemberNames())
            {
                var columnValue = rowData[columnName];

                if (columnValue.GetType().IsArray)
                {
                    Array columnValueArray = (Array)columnValue;
                    for (var i = 0; i < columnValueArray.Length; ++i)

                        ((DataRowView)e.NewItem)[columnName + i] = columnValueArray.GetValue(i);
                }
                else
                    ((DataRowView)e.NewItem)[columnName] = columnValue;
            }
        }


        public void ReloadDataView()
        {
            RunOperationAsync(new ReloadDataViewOperation());
        }

        public void RunOperationAsync(IRequest request, bool reload = false)
        {
            if (request is ProgressReportingRequest reporter)
            {
                reporter.ProgressReporter = _progressReporter;
            }
            Task.Run(() => 
            {
                _mediator.Send(request).ContinueWith((_) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        txtOperation.Text = "";
                        ProgressBar.Value = 0;
                        ProgressBar.IsIndeterminate = false;
                        if (reload)
                        {
                            ReloadDataView();
                        }
                    });
                });
            });
        }
    }
}
