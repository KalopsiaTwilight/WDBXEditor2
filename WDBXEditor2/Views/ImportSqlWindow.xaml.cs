using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WDBXEditor2.Core.Operations;
using WDBXEditor2.Misc;

namespace WDBXEditor2.Views
{

    public enum SqlImportType
    {
        SQLite,
        MySql
    }

    public partial class ImportSqlWindow : Window
    {
        private SqlImportType importType = SqlImportType.MySql;
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

            string lastDbFile = _settings.Get(Constants.LastExportSqliteDbFileNameKey) ?? _settings.Get(Constants.LastImportSqliteDbFileNameKey);
            if (lastDbFile != null)
            {
                tbDatabaseFile.Text = lastDbFile;
                LoadSQLiteTables();
            }

            string lastTableName = string.Empty;
            switch (importType)
            {
                case SqlImportType.MySql:
                    {
                        lastTableName = _settings.Get(Constants.LastImportMysqlDbTableKeyPrefix + _mainWindow.CurrentOpenDB2);
                        lastTableName ??= _settings.Get(Constants.LastExportMysqlDbTableKeyPrefix + _mainWindow.CurrentOpenDB2);
                        break;
                    }
                case SqlImportType.SQLite:
                    {
                        lastTableName = _settings.Get(Constants.LastImportSQliteDbTableKeyPrefix + _mainWindow.CurrentOpenDB2);
                        lastTableName ??= _settings.Get(Constants.LastExportSQliteDbTableKeyPrefix + _mainWindow.CurrentOpenDB2);
                        break;
                    }

            }
            if (!string.IsNullOrEmpty(lastTableName))
            {
                ddlTableName.Text = lastTableName;
            }
            else
            {
                ddlTableName.Text = _mainWindow.CurrentOpenDB2.ToLower();
            }
        }


        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            var success = false;
            switch (importType)
            {
                case SqlImportType.MySql:
                    {
                        success = ImportFromMySqlDatabase();
                        break;
                    }
                case SqlImportType.SQLite:
                    {
                        success = ImportFromSqliteDatabase();
                        break;
                    }
            }
            if (success)
            {
                switch (importType)
                {
                    case SqlImportType.SQLite:
                        {
                            _settings.Store(Constants.LastImportSQliteDbTableKeyPrefix + _mainWindow.CurrentOpenDB2, ddlTableName.Text);
                            break;
                        }
                    case SqlImportType.MySql:
                        {
                            _settings.Store(Constants.LastImportMysqlDbTableKeyPrefix + _mainWindow.CurrentOpenDB2, ddlTableName.Text);
                            SaveDbPassword();
                            break;
                        }
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
                    "Import from Mysql requires a valid database connection and database selected.",
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

        private bool ImportFromSqliteDatabase()
        {
            if (string.IsNullOrEmpty(ddlTableName.Text))
            {
                MessageBox.Show(
                    "Import from SQLite requires a valid database file and table selected.",
                    "WDBXEditor2",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return false;
            }

            var dbcdStorage = _mainWindow.OpenedDB2Storage;
            _mainWindow.RunOperationAsync(new ImportFromSQliteDatabaseOperation()
            {
                Storage = _mainWindow.OpenedDB2Storage,
                TableName = ddlTableName.Text,
                FileName = tbDatabaseFile.Text,
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
                            importType = SqlImportType.MySql;
                            Height = 524;
                            pnlDBFile.Visibility = Visibility.Collapsed;
                            pnlDbConnection.Visibility = Visibility.Visible;
                            LoadDatabases();
                            LoadTables(ddlDatabase.Text);
                            break;
                        }
                    case "SQLite Database":
                        {
                            importType = SqlImportType.SQLite;
                            Height = 332;
                            pnlDBFile.Visibility = Visibility.Visible;
                            pnlDbConnection.Visibility = Visibility.Collapsed;
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
            if (e.AddedItems.Count > 0)
            {
                var newDatabase = e.AddedItems[0] as string;
                _settings.Store(Constants.LastImportMySqlDbNameKey, newDatabase);
                LoadTables(newDatabase);
            }
        }
        private void btnSelectSQLiteDb_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer),
                CheckFileExists = false
            };

            if (openFileDialog.ShowDialog() == true)
            {
                tbDatabaseFile.Text = openFileDialog.FileName;
                _settings.Store(Constants.LastExportSqliteDbFileNameKey, openFileDialog.FileName);
                LoadTables(ddlDatabase.Text);
            }
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

            switch (importType)
            {
                case SqlImportType.MySql:
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
            switch (importType)
            {
                case SqlImportType.MySql:
                    {
                        LoadMySqlTables(database);
                        break;
                    }
                case SqlImportType.SQLite:
                    {
                        LoadSQLiteTables();
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
                        foreach (var table in tables)
                        {
                            ddlTableName.Items.Add(table);
                        }
                        ddlTableName.IsEnabled = true;
                        var lastSelected = _settings.Get(Constants.LastImportMysqlDbTableKeyPrefix + _mainWindow.CurrentOpenDB2);
                        lastSelected ??= _settings.Get(Constants.LastExportMysqlDbTableKeyPrefix + _mainWindow.CurrentOpenDB2);
                        if (lastSelected != null && ddlTableName.Items.Contains(lastSelected))
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

        private void LoadSQLiteTables()
        {
            var connBuilder = new SqliteConnectionStringBuilder
            {
                DataSource = tbDatabaseFile.Text
            };
            Task.Run(() =>
            {
                try
                {
                    var tables = new List<string>();
                    var sqlConnection = new SqliteConnection(connBuilder.ConnectionString);
                    var command = sqlConnection.CreateCommand();
                    command.CommandText = "SELECT name FROM sqlite_master WHERE type='table';";
                    sqlConnection.Open();
                    var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        tables.Add(reader.GetString(0));
                    }
                    sqlConnection.Close();
                    Dispatcher.Invoke(() =>
                    {
                        foreach (var table in tables)
                        {
                            ddlTableName.Items.Add(table);
                        }
                        ddlTableName.IsEnabled = true;
                        var lastSelected = _settings.Get(Constants.LastImportSQliteDbTableKeyPrefix + _mainWindow.CurrentOpenDB2);
                        lastSelected ??= _settings.Get(Constants.LastExportSQliteDbTableKeyPrefix + _mainWindow.CurrentOpenDB2);
                        if (lastSelected != null && ddlTableName.Items.Contains(lastSelected))
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
                catch (SqliteException e)
                {
                    if (e.SqliteErrorCode == 26)
                    {
                        MessageBox.Show("Selected file was not a valid SQLite database file.");
                        Dispatcher.Invoke(() =>
                        {
                            tbDatabaseFile.Text = string.Empty;
                        });
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

        private void SaveDbPassword()
        {
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
        }
    }
}
