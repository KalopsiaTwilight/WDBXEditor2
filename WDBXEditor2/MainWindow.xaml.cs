using DBCD;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WDBXEditor2.Controller;
using WDBXEditor2.Core.Operations;
using WDBXEditor2.Views;
using WDBXEditor2.Core;
using WDBXEditor2.Operations;
using System.Threading.Tasks;
using DBCD.IO;
using System.Collections.ObjectModel;
using WDBXEditor2.Misc;

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
        public ObservableCollection<DBCDRowProxy> DataGridSource { get; set; } = new();

        public ColumnInfo SelectedColumnInfo { get; set; } = new();

        public Filter Filter { get; set; } = new();
        public IDBCDStorage OpenedDB2Storage { get; set; }

        private readonly IServiceProvider _serviceProvider;
        private IMediator _mediator;
        private IProgressReporter _progressReporter;


        private int _copiedRowId = -1;

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

                tbCurrentDb2Stats.Text = $"{storage.Count} rows, {DBCDHelper.GetColumnNames(storage).Length} columns";
                tbCurrentFile.Text = CurrentOpenDB2 + ".db2";
                tbCurrentDefinition.Text = storage.LayoutHash.ToString("X8");
                _copiedRowId = -1;

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

            _copiedRowId = -1;
            CurrentOpenDB2 = string.Empty;
            OpenedDB2Storage = null;

            tbCurrentDb2Stats.Text = string.Empty;
            tbCurrentFile.Text = string.Empty;
            tbCurrentDefinition.Text = string.Empty;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(CurrentOpenDB2))
            {
                RunOperationAsync(new SaveDb2ToFileOperation()
                {
                    Storage = OpenedDB2Storage,
                    FileName = OpenedDB2Paths[CurrentOpenDB2]
                });
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
                RunOperationAsync(new SaveDb2ToFileOperation()
                {
                    Storage = OpenedDB2Storage,
                    FileName = saveFileDialog.FileName
                });
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
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
            OpenWindow<ExportSqlWindow>();
        }

        private void ImportSql_Click(object sender, RoutedEventArgs e)
        {
            OpenWindow<ImportSqlWindow>();
        }

        private void SetColumn_Click(object sender, RoutedEventArgs e)
        {
            OpenWindow<SetColumnWindow>();
        }

        private void ReplaceColumn_Click(object sender, RoutedEventArgs e)
        {
            OpenWindow<ReplaceColumnWindow>();
        }

        private void SetBitColumn_Click(object sender, RoutedEventArgs e)
        {
            OpenWindow<SetFlagWindow>();
        }

        private void SetDependentColumn_Click(object sender, RoutedEventArgs e)
        {
            OpenWindow<SetDependentColumnWindow>();
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            _copiedRowId = (DB2DataGrid.SelectedItem as DBCDRowProxy)?.RowData?.ID ?? -1;
        }

        private void Paste_Click(object sender, RoutedEventArgs e)
        {
            if (_copiedRowId == -1)
            {
                return;
            }

            if (!OpenedDB2Storage.ContainsKey(_copiedRowId))
            {
                _copiedRowId = -1;
                return;
            }

            var rowToCopy = OpenedDB2Storage[_copiedRowId];
            var newRow = DBCDHelper.ConstructNewRow(OpenedDB2Storage);

            var columns = DBCDHelper.GetColumnNames(OpenedDB2Storage);
            var idField = DBCDHelper.GetIdFieldName(OpenedDB2Storage);
            foreach(var col in columns)
            {
                if (col != idField)
                {
                    var copyVal = DBCDHelper.GetDBCRowColumn(rowToCopy, col);
                    DBCDHelper.SetDBCRowColumn(newRow, col, copyVal);
                }
            }
            OpenedDB2Storage[newRow.ID] = newRow;
            var proxy = new DBCDRowProxy(newRow);
            DataGridSource.Add(proxy);
            DB2DataGrid.ScrollIntoView(proxy);
            DB2DataGrid.SelectedItem = proxy;
        }
        private void Find_Click(object sender, RoutedEventArgs e)
        {
            OpenWindow<FindColumnWindow>();
        }

        private void ClearFilter_Click(object sender, RoutedEventArgs e)
        {
            Filter = new();
            ReloadDataView();
        }

        public void ReloadDataView()
        {
            RunOperationAsync(new ReloadDataViewOperation());
        }

        private void OpenWindow<T>() where T : Window
        {
            if (string.IsNullOrEmpty(CurrentOpenDB2))
                return;

            ActivatorUtilities.CreateInstance<T>(_serviceProvider).Show();
        }

        public void BlockUI()
        {
            Dispatcher.Invoke(() =>
            {
                mainMenu.IsEnabled = false;
                DB2DataGrid.IsReadOnly = true;
                OpenDBItems.IsEnabled = false;
            });
        }

        public void UnblockUI()
        {
            Dispatcher.Invoke(() =>
            {
                mainMenu.IsEnabled = true;
                DB2DataGrid.IsReadOnly = false;
                OpenDBItems.IsEnabled = true;
            });
        }

        public void RunOperationAsync(IRequest request, bool reload = false)
        {
            if (request is ProgressReportingRequest reporter)
            {
                reporter.ProgressReporter = _progressReporter;
            }
            BlockUI();
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
                        UnblockUI();
                    });
                });
            });
        }

        private void CommandBinding_CanExecute(object sender, System.Windows.Input.CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = mainMenu.IsEnabled;
        }
        private void DB2DataGrid_CurrentCellChanged(object sender, EventArgs e)
        {
            var cell = (sender as DataGrid).CurrentCell;
            if (!cell.IsValid)
            {
                return;
            }
            var columnName = cell.Column.Header.ToString();
            SelectedColumnInfo = DBCDHelper.GetColumnInfo(DBCDHelper.GetUnderlyingType(OpenedDB2Storage), columnName);
            tbColumnInfo.Text = SelectedColumnInfo.ToString(); 
        }

        private void DB2DataGrid_CopyingRowClipboardContent(object sender, DataGridRowClipboardEventArgs e)
        {
            _copiedRowId = (DB2DataGrid.SelectedItem as DBCDRowProxy)?.RowData?.ID ?? -1;
        }
    }
}
