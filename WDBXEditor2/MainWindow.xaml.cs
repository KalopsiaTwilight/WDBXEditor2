using CsvHelper;
using CsvHelper.Configuration;
using DBCD;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WDBXEditor2.Controller;
using WDBXEditor2.Helpers;
using WDBXEditor2.Misc;
using WDBXEditor2.Views;

namespace WDBXEditor2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly DBLoader dbLoader = new();
        public string CurrentOpenDB2 { get; set; } = string.Empty;

        public Dictionary<string, string> OpenedDB2Paths { get; set; } = new Dictionary<string, string>();
        public IDBCDStorage OpenedDB2Storage { get; set; }

        private List<DBCDRow> _currentOrderedRows = new();

        public MainWindow()
        {
            InitializeComponent();
            SettingStorage.Initialize();

            Exit.Click += (e, o) => Close();

            Title = $"WDBXEditor2  -  {Constants.Version}";
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

                foreach (string loadedDB in dbLoader.LoadFiles(files))
                {
                    OpenedDB2Paths[loadedDB] = files.First(x => Path.GetFileNameWithoutExtension(x) == loadedDB);
                    OpenDBItems.Items.Add(loadedDB);
                }
            }
        }

        private void OpenDBItems_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Clear DataGrid
            DB2DataGrid.Columns.Clear();
            DB2DataGrid.ItemsSource = new List<string>();

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
        /// Populate the DataView with the DB2 Columns.
        /// </summary>
        private void PopulateColumns(IDBCDStorage storage, ref DataTable data)
        {
            var firstItem = storage.Values.FirstOrDefault();
            if (firstItem == null)
            {
                return;
            }

            foreach (string columnName in firstItem.GetDynamicMemberNames())
            {
                var columnValue = firstItem[columnName];

                if (columnValue.GetType().IsArray)
                {
                    Array columnValueArray = (Array)columnValue;
                    for (var i = 0; i < columnValueArray.Length; ++i)
                        data.Columns.Add(columnName + i);
                }
                else
                    data.Columns.Add(columnName);
            }
        }

        /// <summary>
        /// Populate the DataView with the DB2 Data.
        /// </summary>
        private void PopulateDataView(IDBCDStorage storage, ref DataTable data)
        {
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

            CurrentOpenDB2 = string.Empty;
            OpenedDB2Storage = null;
            _currentOrderedRows = null;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(CurrentOpenDB2))
                dbLoader.LoadedDBFiles[CurrentOpenDB2].Save(OpenedDB2Paths[CurrentOpenDB2]);
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
                        dbcRow[colName] = ConvertHelper.ConvertValue(dbcRow.GetUnderlyingType(), colName, newVal.Text);
                    }
                    catch(Exception exc)
                    {
                        newVal.Text = dbcRow[colName].ToString();
                        var exceptionWindow = new ExceptionWindow();
                        var fieldType = ConvertHelper.GetFieldType(dbcRow.GetUnderlyingType(), colName);

                        exceptionWindow.DisplayException(exc.InnerException ?? exc, $"An error occured setting this value for this cell. This is likely due to an invalid value for conversion to '{fieldType.Name}':");
                        exceptionWindow.Show();
                    }

                    Console.WriteLine($"RowIdx: {rowIdx} Text: {newVal.Text}");
                }
            }
        }

        private void Export_Click(object sender, RoutedEventArgs e)
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
                ExportToCsv(saveFileDialog.FileName);
            }
        }

        private void Import_Click(object sender, RoutedEventArgs e)
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
                ImportCsv(fileName);
                ReloadDataView();
            }
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

        public void ReloadDataView()
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            var data = new DataTable();
            PopulateColumns(OpenedDB2Storage, ref data);
            if (OpenedDB2Storage.Values.Count > 0)
                PopulateDataView(OpenedDB2Storage, ref data);

            stopWatch.Stop();
            Console.WriteLine($"Populating Grid: {CurrentOpenDB2} Elapsed Time: {stopWatch.Elapsed}");
            data.RowDeleting += Data_RowDeleted;
            DB2DataGrid.ItemsSource = data.DefaultView;
        }

        private void Data_RowDeleted(object sender, DataRowChangeEventArgs e)
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

        private void ExportToCsv(string filename)
        {
            var firstItem = OpenedDB2Storage.Values.FirstOrDefault();
            if (firstItem == null)
            {
                return;
            }

            var columnNames = firstItem.GetDynamicMemberNames()
                .SelectMany(x =>
                {
                    var columnData = firstItem[x];
                    if (columnData.GetType().IsArray)
                    {
                        var result = new string[((Array)columnData).Length];
                        for (int i = 0; i < result.Length; i++)
                        {
                            result[i] = x + i;
                        }
                        return result;
                    }
                    return new[] { x };
                });
            using (var fileStream = File.Create(filename))
            using (var writer = new StreamWriter(fileStream))
            {
                writer.WriteLine(string.Join(",", columnNames));
                using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    MemberTypes = CsvHelper.Configuration.MemberTypes.Fields,
                    HasHeaderRecord = false,
                    ShouldQuote = (args) =>
                    {
                        return args.FieldType == typeof(string);
                    }
                }))
                {
                    csv.Context.TypeConverterCache.RemoveConverter<byte[]>();
                    csv.WriteRecords(OpenedDB2Storage.Values);
                }
            }
        }

        private void ImportCsv(string fileName)
        {
            using (var reader = new StreamReader(fileName))
            using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                MemberTypes = CsvHelper.Configuration.MemberTypes.Fields,
                HasHeaderRecord = true,

            }))
            {
                var underlyingType = OpenedDB2Storage.GetType().GenericTypeArguments[0];

                csv.Context.TypeConverterCache.RemoveConverter<byte[]>();
                var records = csv.GetRecords(underlyingType);
                OpenedDB2Storage.Clear();
                foreach (var record in records)
                {
                    var id = (int)underlyingType.GetField(OpenedDB2Storage.AvailableColumns.First()).GetValue(record);
                    var row = OpenedDB2Storage.ConstructRow(id);
                    var fields = underlyingType.GetFields();
                    var arrayFields = fields.Where(x => x.FieldType.IsArray);
                    foreach (var field in fields)
                    {
                        if (field.FieldType.IsArray)
                        {
                            var count = csv.HeaderRecord.Where(x => x.StartsWith(field.Name)).ToList().Count();
                            var rowRecords = new string[count];
                            Array.Copy(csv.Parser.Record, Array.IndexOf(csv.HeaderRecord, field.Name + 0), rowRecords, 0, count);
                            row[field.Name] = ConvertHelper.ConvertArray(field.FieldType, count, rowRecords);
                        } else
                        {
                            row[field.Name] = field.GetValue(record);
                        }
                    }
                    OpenedDB2Storage.Add(id, row);
                }
            }
        }
    }
}
