using Microsoft.Data.Sqlite;
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
    public enum SqlExportType
    {
        SQLite,
        MySql,
        File
    }

    public partial class ExportSqlWindow : Window
    {
        private SqlExportType exportType = SqlExportType.File;
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
            }
            else
            {
                tbHostname.Text = "localhost";
            }

            string lastDbPort = _settings.Get(Constants.LastExportMySqlDbPortKey) ?? _settings.Get(Constants.LastImportMySqlDbPortKey);
            if (lastDbPort != null)
            {
                tbPort.Text = lastDbPort;
            }
            else
            {
                tbPort.Text = "3306";
            }

            string lastDbUsername = _settings.Get(Constants.LastExportMySqlDbUserKey) ?? _settings.Get(Constants.LastImportMySqlDbUserKey);
            if (lastDbUsername != null)
            {
                tbUsername.Text = lastDbUsername;
            }
            else
            {
                tbUsername.Text = "root";
            }

            string lastDbPassword = _settings.Get(Constants.LastExportMySqlDbPasswordKey) ?? _settings.Get(Constants.LastImportMySqlDbPasswordKey);
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
            switch (exportType)
            {
                case SqlExportType.MySql:
                    {
                        lastTableName = _settings.Get(Constants.LastExportMysqlDbTableKeyPrefix + _mainWindow.CurrentOpenDB2);
                        lastTableName ??= _settings.Get(Constants.LastImportMysqlDbTableKeyPrefix + _mainWindow.CurrentOpenDB2);
                        break;
                    }
                case SqlExportType.SQLite:
                    {
                        lastTableName = _settings.Get(Constants.LastExportSQliteDbTableKeyPrefix + _mainWindow.CurrentOpenDB2);
                        lastTableName ??= _settings.Get(Constants.LastImportSQliteDbTableKeyPrefix + _mainWindow.CurrentOpenDB2);
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
            switch (exportType)
            {
                case SqlExportType.File:
                    {
                        success = SaveToSqlFile();
                        break;
                    }
                case SqlExportType.MySql:
                    {
                        success = SaveToMysqlDatabase();
                        break;
                    }
                case SqlExportType.SQLite:
                    {
                        success = SaveToSQLiteDatabase();
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
            var saveFileDialog = new SaveFileDialog
            {
                FileName = Path.GetFileNameWithoutExtension(_mainWindow.CurrentOpenDB2) + ".sql",
                Filter = "SQL Script (*.sql)|*.sql",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer)
            };


            var isSuccess = saveFileDialog.ShowDialog() == true;
            if (isSuccess)
            {
                _settings.Store(Constants.LastExportSQliteDbTableKeyPrefix + _mainWindow.CurrentOpenDB2, ddlTableName.Text);
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

            _settings.Store(Constants.LastExportMysqlDbTableKeyPrefix + _mainWindow.CurrentOpenDB2, ddlTableName.Text);
            SaveDbPassword();
            return true;
        }

        private bool SaveToSQLiteDatabase()
        {
            if (string.IsNullOrEmpty(tbDatabaseFile.Text))
            {
                MessageBox.Show(
                    "Export to SQLite requires a database file being selected.",
                    "WDBXEditor2",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return false;
            }

            var dbcdStorage = _mainWindow.OpenedDB2Storage;
            _mainWindow.RunOperationAsync(new ExportToSQLiteDatabaseOperation()
            {
                CreateTable = cbCreateTable.IsChecked == true,
                DropTable = cbDropTable.IsChecked == true,
                ExportData = cbExportData.IsChecked == true,
                Storage = _mainWindow.OpenedDB2Storage,
                TableName = ddlTableName.Text,
                InsertsPerStatement = uint.Parse(tbNrInserts.Text),
                FileName = tbDatabaseFile.Text
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
                            Height = 360;
                            pnlDBFile.Visibility = Visibility.Collapsed;
                            pnlDbConnection.Visibility = Visibility.Collapsed;
                            exportType = SqlExportType.File;
                            break;
                        }
                    case "MySQL Database":
                        {
                            Height = 610;
                            pnlDBFile.Visibility = Visibility.Collapsed;
                            pnlDbConnection.Visibility = Visibility.Visible;
                            exportType = SqlExportType.MySql;
                            LoadDatabases();
                            LoadTables(ddlDatabase.Text);
                            break;
                        }
                    case "SQLite Database":
                        {
                            Height = 416;
                            pnlDBFile.Visibility = Visibility.Visible;
                            pnlDbConnection.Visibility = Visibility.Collapsed;
                            exportType = SqlExportType.SQLite;
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
            if (e.AddedItems.Count > 0)
            {
                var newDatabase = e.AddedItems[0] as string;
                _settings.Store(Constants.LastExportMySqlDbNameKey, newDatabase);
                LoadTables(newDatabase);
            };
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
                _settings.Store(Constants.LastExportMySqlDbPasswordKey, tbPassword.Password);
            }
        }

        private void LoadDatabases()
        {
            ddlDatabase.Items.Clear();
            ddlDatabase.IsEnabled = false;

            if (string.IsNullOrEmpty(tbPassword.Password))
            {
                return;
            }

            switch (exportType)
            {
                case SqlExportType.MySql:
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
            switch (exportType)
            {
                case SqlExportType.MySql:
                    {
                        LoadMySqlTables(database);
                        break;
                    }
                case SqlExportType.SQLite:
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
                        var lastSelected = _settings.Get(Constants.LastExportMySqlDbNameKey) ?? _settings.Get(Constants.LastImportMySqlDbNameKey);
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
                        var lastSelected = _settings.Get(Constants.LastExportSQliteDbTableKeyPrefix + _mainWindow.CurrentOpenDB2);
                        lastSelected ??= _settings.Get(Constants.LastImportSQliteDbTableKeyPrefix + _mainWindow.CurrentOpenDB2);
                        if (lastSelected != null)
                        {
                            ddlTableName.Text = lastSelected;
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
                    } else
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
