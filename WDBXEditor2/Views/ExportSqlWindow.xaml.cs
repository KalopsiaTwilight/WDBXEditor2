using Microsoft.Win32;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WDBXEditor2.Core.Operations;
using WDBXEditor2.Misc;

namespace WDBXEditor2.Views
{

    public partial class ExportSqlWindow : Window
    {
        private string selectedExportType = "File";
        private readonly MainWindow _mainWindow;
        private readonly ISettingsStorage _settings;

        public ExportSqlWindow(MainWindow mainWindow, ISettingsStorage settings)
        {
            _mainWindow = mainWindow;
            _settings = settings;
            InitializeComponent();

            LoadFormValuesFromSettingStorage();
        }

        private void LoadFormValuesFromSettingStorage()
        {
            string lastExportTypeIndexStr = _settings.Get(Constants.SqlExportTypeStorageKey);
            if (lastExportTypeIndexStr != null)
            {
                ddlExportType.SelectedIndex = int.Parse(lastExportTypeIndexStr);
            }

            string lastDbHost = _settings.Get(Constants.LastExportMySqlDbHostnameKey) ?? _settings.Get(Constants.LastImportMySqlDbHostnameKey);
            if (lastDbHost != null)
            {
                tbHostname.Text = lastDbHost;
            } else
            {
                tbHostname.Text = "localhost";
            }

            string lastDbPort = _settings.Get(Constants.LastExportMySqlDbPortKey) ?? _settings.Get(Constants.LastImportMySqlDbPortKey);
            if (lastDbPort != null)
            {
                tbPort.Text = lastDbPort;
            } else
            {
                tbPort.Text = "3306";
            }

            string lastDbUsername = _settings.Get(Constants.LastExportMySqlDbUserKey) ?? _settings.Get(Constants.LastImportMySqlDbUserKey);
            if (lastDbUsername != null)
            {
                tbUsername.Text = lastDbUsername;
            } else
            {
                tbUsername.Text = "root";
            }

            string lastDbPassword = _settings.Get(Constants.LastExportMySqlDbPasswordKey) ?? _settings.Get(Constants.LastImportMySqlDbPasswordKey);
            if (lastDbPassword != null)
            {
                tbPassword.Password = lastDbPassword;                
            }

            var lastTableName = _settings.Get(Constants.LastExportMysqlDbTableKeyPrefix + _mainWindow.CurrentOpenDB2);
            lastTableName ??= _settings.Get(Constants.LastImportMysqlDbTableKeyPrefix + _mainWindow.CurrentOpenDB2);
            if (lastTableName != null)
            {
                ddlTableName.Text = lastTableName;
            } else
            {
                ddlTableName.Text = _mainWindow.CurrentOpenDB2.ToLower();
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            var success = false;
            switch (selectedExportType)
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
                _settings.Store(Constants.LastExportMysqlDbTableKeyPrefix + _mainWindow.CurrentOpenDB2, ddlTableName.Text);

                var storeDbPass = _settings.Get(Constants.StoreDbPasswords);
                if (storeDbPass == null)
                {
                    var savePassResult = MessageBox.Show(
                        "Would you like to store the last used password for database connections for future exports and imports?\n\nNOTE: Passwords will be stored as plaintext, meaning they will be readable from this application's settings storage by others.",
                        "Store database passwords?", MessageBoxButton.YesNo, MessageBoxImage.Information
                    );
                    if (savePassResult == MessageBoxResult.Yes)
                    {
                        _settings.Store(Constants.StoreDbPasswords, "true");
                        storeDbPass = "true";
                    }
                    else
                    {
                        _settings.Store(Constants.StoreDbPasswords, "false");
                        storeDbPass = "false";
                    }
                }
                if (bool.Parse(storeDbPass))
                {
                    _settings.Store(Constants.LastExportMySqlDbPasswordKey, tbPassword.Password);
                }
                Close();
            }
        }
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private bool SaveToSqlFile()
        {
            var saveFileDialog = new SaveFileDialog
            {
                FileName = Path.GetFileNameWithoutExtension(_mainWindow.CurrentOpenDB2) + ".sql",
                Filter = "SQL Script (*.sql)|*.sql",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer)
            };


            var isSuccess = saveFileDialog.ShowDialog() == true;
            if (isSuccess)
            {
                _mainWindow.RunOperationAsync(new ExportToSqlFileOperation()
                {
                    CreateTable = cbCreateTable.IsChecked == true,
                    DropTable = cbDropTable.IsChecked == true,
                    ExportData = cbExportData.IsChecked == true,
                    FileName = saveFileDialog.FileName,
                    Storage = _mainWindow.OpenedDB2Storage,
                    TableName = ddlTableName.Text,
                    InsertsPerStatement = uint.Parse(tbNrInserts.Text)
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
            _mainWindow.RunOperationAsync(new ExportToMysqlDatabaseOperation()
            {
                CreateTable = cbCreateTable.IsChecked == true,
                DropTable = cbDropTable.IsChecked == true,
                ExportData = cbExportData.IsChecked == true,
                Storage = _mainWindow.OpenedDB2Storage,
                TableName = ddlTableName.Text,
                InsertsPerStatement = uint.Parse(tbNrInserts.Text),
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
                _settings.Store(Constants.SqlExportTypeStorageKey, ddlExportType.SelectedIndex.ToString());

                switch (selectedExportType)
                {
                    case "File":
                        {
                            Height = 362;
                            pnlDbConnection.Visibility = Visibility.Collapsed;
                            break;
                        }
                    case "MySQL Database":
                        {
                            Height = 624;
                            pnlDbConnection.Visibility = Visibility.Visible;
                            LoadDatabases();
                            LoadTables(ddlDatabase.Text);
                            break;
                        }
                }
            }
        }

        private void tbHostname_TextChanged(object sender, TextChangedEventArgs e)
        {
            _settings.Store(Constants.LastExportMySqlDbHostnameKey, tbHostname.Text);
            LoadDatabases();
        }

        private void tbPort_TextChanged(object sender, TextChangedEventArgs e)
        {
            _settings.Store(Constants.LastExportMySqlDbPortKey, tbPort.Text);
            LoadDatabases();
        }

        private void tbUsername_TextChanged(object sender, TextChangedEventArgs e)
        {
            _settings.Store(Constants.LastExportMySqlDbUserKey, tbUsername.Text);
            LoadDatabases();
        }

        private void DbPassword_Changed(object sender, RoutedEventArgs e)
        {
            LoadDatabases();
        }

        private void ddlDatabase_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var newDatabase = e.AddedItems[0] as string;
            _settings.Store(Constants.LastExportMySqlDbNameKey, newDatabase);
            LoadTables(newDatabase);
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
                        var lastSelected = _settings.Get(Constants.LastExportMySqlDbNameKey) ?? _settings.Get(Constants.LastImportMySqlDbNameKey);
                        if (lastSelected != null && ddlDatabase.Items.Contains(lastSelected))
                        {
                            ddlDatabase.Text = lastSelected;
                            ddlDatabase.SelectedItem = lastSelected;
                        } else
                        {
                            ddlDatabase.SelectedIndex = 0;
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
                        var lastSelected = _settings.Get(Constants.LastExportMysqlDbTableKeyPrefix + _mainWindow.CurrentOpenDB2);
                        lastSelected ??= _settings.Get(Constants.LastImportMysqlDbTableKeyPrefix + _mainWindow.CurrentOpenDB2);
                        if (lastSelected != null)
                        {
                            ddlTableName.Text = lastSelected;
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
