using Microsoft.Win32;
using MySql.Data.MySqlClient;
using Org.BouncyCastle.Asn1.Ocsp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
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

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            var success = false;
            switch (ddlExportType.Text)
            {
                case "File":
                    {
                        success = SaveToSqlFile();
                        break;
                    }
                case "MySQL Database":
                    {
                        success = SaveToMysqlDatabase();
                        break;
                    }
            }
            if (success)
            {
                Close();
            }
        }
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private bool SaveToSqlFile()
        {
            var dbcdStorage = _mainWindow.OpenedDB2Storage;
            var saveFileDialog = new SaveFileDialog
            {
                FileName = Path.GetFileNameWithoutExtension(_mainWindow.CurrentOpenDB2) + ".sql",
                Filter = "SQL Script (*.sql)|*.sql",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer)
            };


            var isSuccess = saveFileDialog.ShowDialog() == true;
            if (isSuccess)
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
            return isSuccess;
        }

        private bool SaveToMysqlDatabase()
        { 
            if (string.IsNullOrEmpty(ddlDatabase.Text))
            {
                MessageBox.Show(
                    "Export to Mysql requires a valid database connection and database selected.",
                    "WDBXEditor2",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return false;
            }

            var dbcdStorage = _mainWindow.OpenedDB2Storage;
            _mainWindow.RunOperationAsync("Exporting to MySQL database", new ExportToMysqlDatabaseOperation()
            {
                CreateTable = cbCreateTable.IsChecked == true,
                DropTable = cbDropTable.IsChecked == true,
                ExportData = cbExportData.IsChecked == true,
                Storage = _mainWindow.OpenedDB2Storage,
                TableName = ddlTableName.Text,
                DatabaseHost = tbHostname.Text,
                DatabasePort = uint.Parse(tbPort.Text),
                DatabaseName = ddlDatabase.Text,
                DatabaseUser = tbUsername.Text,
                DatabasePassword = tbPassword.Password
            });
            return true;
        }


        private void ddlExportType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var newExportType = (e.AddedItems[0] as ComboBoxItem)?.Content?.ToString();
            if (selectedExportType != newExportType)
            {
                selectedExportType = newExportType;
                _settings.Store(Constants.ExportTypeStorageKey, ddlExportType.SelectedIndex.ToString());

                switch (selectedExportType)
                {
                    case "File":
                        {
                            Height = 338;
                            pnlDbConnection.Visibility = Visibility.Collapsed;
                            break;
                        }
                    case "MySQL Database":
                        {
                            Height = 600;
                            pnlDbConnection.Visibility = Visibility.Visible;
                            LoadDatabases();
                            LoadTables(ddlDatabase.Text);
                            break;
                        }
                }
            }
        }

        private void ddlDatabase_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var newDatabase = e.AddedItems[0] as string;
            LoadTables(newDatabase);
        }

        private void DbPassword_Changed(object sender, RoutedEventArgs e)
        {
            LoadDatabases();
        }

        private void LoadDatabases()
        {
            ddlDatabase.Items.Clear();
            ddlDatabase.IsEnabled = false;
            
            if (string.IsNullOrEmpty(tbPassword.Password))
            {
                return;
            }

            switch (selectedExportType)
            {
                case "MySQL Database":
                    {
                        LoadMySqlDatabases();
                        break;
                    }
            }
        }
        private void LoadTables(string database)
        {
            ddlTableName.Items.Clear();
            if (string.IsNullOrEmpty(tbPassword.Password) || string.IsNullOrEmpty(database))
            {
                return;
            }
            switch (selectedExportType)
            {
                case "MySQL Database":
                    {
                        LoadMySqlTables(database);
                        break;
                    }
            }
        }

        private void LoadMySqlDatabases()
        {
            var connectionString = GetMysqlConnectionString();
            Task.Run(() =>
            {
                try
                {
                    var databases = new List<string>();
                    var sqlConnection = new MySqlConnection(connectionString);
                    var command = sqlConnection.CreateCommand();
                    command.CommandText = "SHOW DATABASES;";
                    sqlConnection.Open();
                    var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        databases.Add(reader.GetString(0));
                    }
                    sqlConnection.Close();
                    Dispatcher.Invoke(() =>
                    {
                        foreach (var database in databases)
                        {
                            ddlDatabase.Items.Add(database);
                        }
                        ddlDatabase.IsEnabled = true;
                    });
                }
                catch (MySqlException ex)
                {
                    if (ex.Number == 1045)
                    {
                        // Invalid auth
                    }
                    else
                    {
                        throw;
                    }
                }
            });
        }

        private void LoadMySqlTables(string database)
        {
            var connectionString = GetMysqlConnectionString(database);
            Task.Run(() =>
            {
                try
                {
                    var tables = new List<string>();
                    var sqlConnection = new MySqlConnection(connectionString);
                    var command = sqlConnection.CreateCommand();
                    command.CommandText = "SHOW TABLES;";
                    sqlConnection.Open();
                    var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        tables.Add(reader.GetString(0));
                    }
                    sqlConnection.Close();
                    Dispatcher.Invoke(() =>
                    {
                        foreach (var database in tables)
                        {
                            ddlTableName.Items.Add(database);
                        }
                    });
                }
                catch (MySqlException ex)
                {
                    if (ex.Number == 1045)
                    {
                        // Invalid auth
                    }
                    else
                    {
                        throw;
                    }
                }
            });
        }

        private string GetMysqlConnectionString(string database = null)
        {
            var connectionBuilder = new MySqlConnectionStringBuilder
            {
                UserID = tbUsername.Text,
                Password = tbPassword.Password,
                Server = tbHostname.Text,
                Port = uint.Parse(tbPort.Text),
                Database = database
            };
            return connectionBuilder.ConnectionString;
        }
    }
}
