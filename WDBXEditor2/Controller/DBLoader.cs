using DBCD;
using DBCD.Providers;
using DBDefsLib;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using WDBXEditor2.Misc;
using WDBXEditor2.Views;
using static DBDefsLib.Structs;

namespace WDBXEditor2.Controller
{
    public class DBLoader
    {
        public ConcurrentDictionary<string, IDBCDStorage> LoadedDBFiles;

        private readonly IDBDProvider _dbdProvider;
        private readonly IDBDNameProvider _dbdNameProvider;
        private readonly IServiceProvider _serviceProvider;

        public DBLoader(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _dbdProvider = serviceProvider.GetService<IDBDProvider>();
            _dbdNameProvider = serviceProvider.GetService<IDBDNameProvider>();
            LoadedDBFiles = new ConcurrentDictionary<string, IDBCDStorage>();
        }

        public string[] LoadFiles(string[] files, string build, Locale locale)
        {
            var loadedFiles = new List<string>();
            Stopwatch stopWatch = null;

            foreach (string db2Path in files)
            {
                string db2Name = GetDb2Name(db2Path);

                if (string.IsNullOrEmpty(db2Name))
                {
                    MessageBox.Show(
                        string.Format("Cant find table definitions for {0}.\nFilename was not recognized as an existing DB2 table. Only files with recognizable table names can be opened at the moment.", db2Path),
                        "WDBXEditor2",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                    continue;
                }

                try
                {
                    var dbcd = new DBCD.DBCD(new FilesystemDBCProvider(Path.GetDirectoryName(db2Path)), _dbdProvider);
                    stopWatch = new Stopwatch();
                    stopWatch.Start();
                    var storage = dbcd.Load(db2Name, build, locale);

                    if (LoadedDBFiles.ContainsKey(db2Name))
                    {
                        loadedFiles.Add(db2Name);
                        LoadedDBFiles[db2Name] = storage;
                    }
                    else if (LoadedDBFiles.TryAdd(db2Name, storage))
                    {
                        loadedFiles.Add(db2Name);
                    }

                    stopWatch.Stop();
                    Console.WriteLine($"Loading File: {db2Name} Elapsed Time: {stopWatch.Elapsed}");
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

        public string GetDb2Name(string filePath)
        {
            return _dbdNameProvider.GetTableNameForFile(Path.GetFileNameWithoutExtension(filePath));
        }

        public VersionDefinitions[] GetVersionDefinitionsForDB2(string db2File)
        {
            var dbdStream = _dbdProvider.StreamForTableName(db2File, null);
            var dbdReader = new DBDReader();
            var databaseDefinition = dbdReader.Read(dbdStream);

            return databaseDefinition.versionDefinitions;
        }
    }
}
