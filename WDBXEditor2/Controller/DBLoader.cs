using DBCD;
using DBCD.Providers;
using DBDefsLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using WDBXEditor2.Views;
using static DBDefsLib.Structs;

namespace WDBXEditor2.Controller
{
    public class DBLoader
    {
        public ConcurrentDictionary<string, IDBCDStorage> LoadedDBFiles;

        private readonly IDBDProvider dbdProvider;

        public DBLoader()
        {
            dbdProvider = new GithubDBDProvider();
            LoadedDBFiles = new ConcurrentDictionary<string, IDBCDStorage>();
        }

        public string[] LoadFiles(string[] files)
        {
            var loadedFiles = new List<string>();
            Stopwatch stopWatch = null;

            foreach (string db2Path in files)
            {
                string db2Name = Path.GetFileNameWithoutExtension(db2Path);

                try
                {
                    DefinitionSelect definitionSelect = new();
                    definitionSelect.SetDB2Name(db2Name);
                    definitionSelect.SetDefinitionFromVersionDefinitions(GetVersionDefinitionsForDB2(db2Name));
                    definitionSelect.ShowDialog();

                    var dbcd = new DBCD.DBCD(new FilesystemDBCProvider(Path.GetDirectoryName(db2Path)), dbdProvider);

                    if (definitionSelect.IsCanceled)
                        continue;

                    stopWatch = new Stopwatch();
                    var storage = dbcd.Load(db2Name, definitionSelect.SelectedVersion, definitionSelect.SelectedLocale);

                    if (LoadedDBFiles.ContainsKey(db2Name))
                    {
                        loadedFiles.Add(db2Name);
                        LoadedDBFiles[db2Name] = storage;
                    }
                    else if (LoadedDBFiles.TryAdd(db2Name, storage))
                        loadedFiles.Add(db2Name);

                    stopWatch.Stop();
                    Console.WriteLine($"Loading File: {db2Name} Elapsed Time: {stopWatch.Elapsed}");
                }
                catch (AggregateException)
                {
                    MessageBox.Show(
                        string.Format("Cant find defenitions for {0}.\nCheck your Filename and note upper and lower case", db2Name),
                        "WDBXEditor2",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    MessageBox.Show(
                        string.Format("Cant load {0}.\n{1}", db2Name, ex.Message),
                        "WDBXEditor2",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                }
            }

            return loadedFiles.ToArray();
        }

        public VersionDefinitions[] GetVersionDefinitionsForDB2(string db2File)
        {
            var dbdStream = dbdProvider.StreamForTableName(db2File, null);
            var dbdReader = new DBDReader();
            var databaseDefinition = dbdReader.Read(dbdStream);

            return databaseDefinition.versionDefinitions;
        }
    }
}
