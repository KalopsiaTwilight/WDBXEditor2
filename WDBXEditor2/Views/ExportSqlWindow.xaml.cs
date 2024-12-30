using DBCD;
using Microsoft.VisualBasic;
using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WDBXEditor2.Helpers;
using WDBXEditor2.Misc;

namespace WDBXEditor2.Views
{

    public partial class ExportSqlWindow : Window
    {
        const string ExportTypeStorageKey = "LastSelectedSQLExportTypeIndex";
        const int InsertsPerStatement = 1000;

        private string selectedExportType = "File";
        private readonly MainWindow _mainWindow;
        private readonly BackgroundWorker _worker;

        public ExportSqlWindow(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            ddlTableName.Text = _mainWindow.CurrentOpenDB2;

            string lastExportTypeIndexStr = SettingStorage.Get(ExportTypeStorageKey);
            if (lastExportTypeIndexStr != null)
            {
                ddlExportType.SelectedIndex = int.Parse(lastExportTypeIndexStr);
            }
            _worker = new BackgroundWorker();
            _worker.WorkerReportsProgress = true;

            _worker.DoWork += BackgroundWorker_DoWork;
            _worker.RunWorkerCompleted += BackGroundWorker_Completed;
            _worker.ProgressChanged += BackgroundWorker_OnProcess;
        }

        private void BackgroundWorker_OnProcess(object sender, ProgressChangedEventArgs e)
        {
            pbProgress.Visibility = Visibility.Visible;
            pbProgress.Value = e.ProgressPercentage;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            _worker.RunWorkerAsync(new RunSQLExportBackgroundArgs() {
                Type = ddlExportType.Text,
                DropTable = cbDropTable.IsChecked == true,
                CreateTable = cbCreateTable.IsChecked == true,
                ExportData = cbExportData.IsChecked == true,
                TableName = ddlTableName.Text
            });
        }

        private void BackGroundWorker_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            Close();
        }

        private void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            var args = (RunSQLExportBackgroundArgs)e.Argument;
            switch (args.Type)
            {
                case "File":
                    {
                        SaveToSqlFile(args);
                        break;
                    }
            }
        }

        private void SaveToSqlFile(RunSQLExportBackgroundArgs args)
        {
            var dbcdStorage = _mainWindow.OpenedDB2Storage;
            var saveFileDialog = new SaveFileDialog
            {
                FileName = Path.GetFileNameWithoutExtension(_mainWindow.CurrentOpenDB2) + ".sql",
                Filter = "SQL Script (*.sql)|*.sql",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer)
            };


            if (saveFileDialog.ShowDialog() == true)
            {
                using (var fileStream = File.Create(saveFileDialog.FileName))
                using (var writer = new StreamWriter(fileStream))
                {
                    if (args.DropTable)
                    {
                        writer.WriteLine($"DROP TABLE IF EXISTS {args.TableName};");
                        writer.WriteLine();
                    }
                    if (args.CreateTable)
                    {
                        WriteTableDefinition(dbcdStorage, writer, args.TableName);
                    }
                    if (args.ExportData)
                    {
                        WriteData(dbcdStorage, writer, args.TableName);
                    }
                }
            }
        }

        private void WriteTableDefinition(IDBCDStorage storage, TextWriter writer, string tableName)
        {
            var underlyingType = storage.GetType().GetGenericArguments()[0];
            var columns = DBCDRowHelper.GetColumnNames(storage);

            writer.WriteLine($"CREATE TABLE {tableName} (");

            for (var i = 0; i < columns.Length; i++)
            {
                if (i > 0)
                {
                    writer.WriteLine(",");
                }
                writer.Write($"  {columns[i]} {GetSqlDataType(DBCDRowHelper.GetFieldType(underlyingType, columns[i]))}");
            }

            writer.WriteLine();
            writer.WriteLine(");");
            writer.WriteLine();
        }

        private void WriteData(IDBCDStorage storage, TextWriter writer, string tableName, string operation = "INSERT")
        {
            var rows = storage.Values;
            var processedCount = 0;
            var colNames = DBCDRowHelper.GetColumnNames(storage);

            while (processedCount < rows.Count)
            {
                var row = rows.ElementAt(processedCount);

                var progress = (int)Math.Floor((float)processedCount / rows.Count * 100);
                _worker.ReportProgress(progress);

                if (processedCount % InsertsPerStatement == 0)
                {
                    if (processedCount != 0)
                    {
                        writer.WriteLine(";");
                        writer.WriteLine();
                    }
                    writer.WriteLine($"{operation} INTO {tableName} ({string.Join(", ", colNames)})");
                    writer.WriteLine("VALUES");
                }
                else
                {
                    writer.WriteLine(",");
                }

                writer.Write($"  ({string.Join(",", colNames.Select(x => GetSqlValue(DBCDRowHelper.GetDBCRowColumn(row, x))))})");
                processedCount++;
            }
            writer.WriteLine(";");
            writer.WriteLine();
        }

        private string GetSqlValue(object val)
        {
            if (val == null)
            {
                return "null";
            }
            if (val is string txtVal)
            {
                return "'" + txtVal.Replace("'", "''") + "'";
            }
            if (val is float floatVal)
            {
                return floatVal.ToString(CultureInfo.InvariantCulture);
            }

            return val.ToString();
        }

        private string GetSqlDataType(Type t)
        {
            switch (t.Name)
            {
                case nameof(UInt64): return "BIGINT UNSIGNED";
                case nameof(Int64): return "BIGINT";
                case nameof(Single): return "FLOAT";
                case nameof(Int32): return "INT";
                case nameof(UInt32): return "INT UNSIGNED";
                case nameof(Int16): return "SMALLINT";
                case nameof(UInt16): return "SMALLINT UNSIGNED";
                case nameof(Byte): return "TINYINT UNSIGNED";
                case nameof(SByte): return "TINYINT";
                case nameof(String): return "TEXT";
            }
            throw new InvalidOperationException("Unknown datatype encounted in SQL conversion: " + t.Name);
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ddlExportType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var newExportType = (e.AddedItems[0] as ComboBoxItem)?.Content?.ToString();
            if (selectedExportType != newExportType)
            {
                selectedExportType = newExportType;
                SettingStorage.Store(ExportTypeStorageKey, ddlExportType.SelectedIndex.ToString());
            }
        }


        private class RunSQLExportBackgroundArgs
        {
            public string Type { get; set; }
            public bool DropTable { get; set; }
            public bool CreateTable { get; set; }
            public bool ExportData { get; set; }
            public string TableName { get; set; }
        }
    }
}
