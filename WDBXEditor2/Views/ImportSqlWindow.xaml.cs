using Microsoft.Win32;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WDBXEditor2.Core.Operations;
using WDBXEditor2.Misc;

namespace WDBXEditor2.Views
{

    public partial class ImportSqlWindow : Window
    {
        private string selectedImportType = "MySQL Database";
        private readonly MainWindow _mainWindow;
        private readonly ISettingsStorage _settings;

        public ImportSqlWindow(MainWindow mainWindow, ISettingsStorage settings)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            _settings = settings;

            LoadFormValuesFromSettingStorage();
        }

        private void LoadFormValuesFromSettingStorage()
        {
            string lastImportTypeIndexStr = _settings.Get(Constants.SqlImportTypeStorageKey);
            if (lastImportTypeIndexStr != null)
            {
                ddlImportType.SelectedIndex = int.Parse(lastImportTypeIndexStr);
            }

            string lastDbHost = _settings.Get(Constants.LastImportMySqlDbHostnameKey) ?? _settings.Get(Constants.LastExportMySqlDbHostnameKey);
            if (lastDbHost != null)
            {
                tbHostname.Text = lastDbHost;
            }
            else
            {
                tbHostname.Text = "localhost";
            }

            string lastDbPort = _settings.Get(Constants.LastImportMySqlDbPortKey) ?? _settings.Get(Constants.LastExportMySqlDbPortKey);
            if (lastDbPort != null)
            {
                tbPort.Text = lastDbPort;
            }
            else
            {
                tbPort.Text = "3306";
            }

            string lastDbUsername = _settings.Get(Constants.LastImportMySqlDbUserKey) ?? _settings.Get(Constants.LastExportMySqlDbUserKey);
            if (lastDbUsername != null)
            {
                tbUsername.Text = lastDbUsername;
            }
            else
            {
                tbUsername.Text = "root";
            }

            string lastDbPassword = _settings.Get(Constants.LastImportMySqlDbPasswordKey) ?? _settings.Get(Constants.LastExportMySqlDbPasswordKey);
            if (lastDbPassword != null)
            {
                tbPassword.Password = lastDbPassword;
            }
        }


        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            var success = false;
            switch (selectedImportType)
            {
                case "MySQL Database":
                    {
                        success = ImportFromMySqlDatabase();
                        break;
                    }
            }
            if (success)
            {
                _settings.Store(Constants.LastImportMysqlDbTableKeyPrefix + _mainWindow.CurrentOpenDB2, ddlTableName.Text);
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
                    _settings.Store(Constants.LastImportMySqlDbPasswordKey, tbPassword.Password);
                }
                Close();
            }
        }
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        private bool ImportFromMySqlDatabase()
        { 
            if (string.IsNullOrEmpty(ddlTableName.Text))
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
            _mainWindow.RunOperationAsync(new ImportFromMysqlDatabaseOperation()
            {
                Storage = _mainWindow.OpenedDB2Storage,
                TableName = ddlTableName.Text,
                DatabaseHost = tbHostname.Text,
                DatabasePort = uint.Parse(tbPort.Text),
                DatabaseName = ddlDatabase.Text,
                DatabaseUser = tbUsername.Text,
                DatabasePassword = tbPassword.Password
            }, true);
            return true;
        }

        private void ddlImportType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var newExportType = (e.AddedItems[0] as ComboBoxItem)?.Content?.ToString();
            if (selectedImportType != newExportType)
            {
                selectedImportType = newExportType;
                _settings.Store(Constants.SqlImportTypeStorageKey, ddlImportType.SelectedIndex.ToString());

                switch (selectedImportType)
                {
                    case "MySQL Database":
                        {
                            Height = 524;
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
            _settings.Store(Constants.LastImportMySqlDbHostnameKey, tbHostname.Text);
            LoadDatabases();
        }

        private void tbPort_TextChanged(object sender, TextChangedEventArgs e)
        {
            _settings.Store(Constants.LastImportMySqlDbPortKey, tbPort.Text);
            LoadDatabases();
        }

        private void tbUsername_TextChanged(object sender, TextChangedEventArgs e)
        {
            _settings.Store(Constants.LastImportMySqlDbUserKey, tbUsername.Text);
            LoadDatabases();
        }

        private void ddlDatabase_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var newDatabase = e.AddedItems[0] as string;
            _settings.Store(Constants.LastImportMySqlDbNameKey, newDatabase);
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

            switch (selectedImportType)
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
            ddlTableName.IsEnabled = false;
            if (string.IsNullOrEmpty(tbPassword.Password) || string.IsNullOrEmpty(database))
            {
                return;
            }
            switch (selectedImportType)
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
                        var lastSelected = _settings.Get(Constants.LastImportMySqlDbNameKey) ?? _settings.Get(Constants.LastExportMySqlDbNameKey);
                        if (lastSelected != null && ddlDatabase.Items.Contains(lastSelected))
                        {
                            ddlDatabase.Text = lastSelected;
                            ddlDatabase.SelectedItem = lastSelected;
                        }
                        else
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
                        ddlTableName.IsEnabled = true;
                        var lastSelected = _settings.Get(Constants.LastImportMysqlDbTableKeyPrefix + _mainWindow.CurrentOpenDB2);
                        lastSelected ??= _settings.Get(Constants.LastExportMysqlDbTableKeyPrefix + _mainWindow.CurrentOpenDB2);
                        if (lastSelected != null && ddlDatabase.Items.Contains(lastSelected))
                        {
                            ddlTableName.Text = lastSelected;
                            ddlTableName.SelectedItem = lastSelected;
                        }
                        else
                        {
                            ddlTableName.SelectedIndex = 0;
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
