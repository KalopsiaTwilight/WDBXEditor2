using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using WDBXEditor2.Core.Operations;
using WDBXEditor2.Misc;

namespace WDBXEditor2.Views
{

    public partial class ExportSqlWindow : Window
    {
        const int InsertsPerStatement = 1000;

        private string selectedExportType = "File";
        private readonly MainWindow _mainWindow;
        private readonly ISettingsStorage _settings;

        public ExportSqlWindow(MainWindow mainWindow, ISettingsStorage settings)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            _settings = settings;

            ddlTableName.Text = _mainWindow.CurrentOpenDB2;

            string lastExportTypeIndexStr = _settings.Get(Constants.ExportTypeStorageKey);
            if (lastExportTypeIndexStr != null)
            {
                ddlExportType.SelectedIndex = int.Parse(lastExportTypeIndexStr);
            }
        }

        private void BackgroundWorker_OnProcess(object sender, ProgressChangedEventArgs e)
        {
            pbProgress.Visibility = Visibility.Visible;
            pbProgress.Value = e.ProgressPercentage;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            switch (ddlExportType.Text)
            {
                case "File":
                    {
                        SaveToSqlFile();
                        break;
                    }
            }
            Close();
        }

        private void BackGroundWorker_Completed(object sender, RunWorkerCompletedEventArgs e)
        {
            Close();
        }


        private void SaveToSqlFile()
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
                _mainWindow.RunOperationAsync("Exporting to SQL File...", new ExportToSqlFileOperation()
                {
                    CreateTable = cbCreateTable.IsChecked == true,
                    DropTable = cbDropTable.IsChecked == true,
                    ExportData = cbExportData.IsChecked == true,
                    FileName = saveFileDialog.FileName,
                    Storage = _mainWindow.OpenedDB2Storage,
                    TableName = ddlTableName.Text,
                });
            }
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
                _settings.Store(Constants.ExportTypeStorageKey, ddlExportType.SelectedIndex.ToString());
            }
        }
    }
}
