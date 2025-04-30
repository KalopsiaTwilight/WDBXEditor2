using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;

namespace WDBXEditor2.Controller
{
    public interface IDBDNameProvider
    {
        public string GetTableNameForFile(string fileName);
    }

    internal class GithubDBDNameProvider: IDBDNameProvider
    {
        HttpClient _httpClient;
        List<string> _tableNames;

        public GithubDBDNameProvider(HttpClient client) {
            _httpClient = client;
            _tableNames = [];

            LoadDBDManifest();
        }


        private void LoadDBDManifest()
        {
            var manifest = _httpClient.GetStringAsync("https://raw.githubusercontent.com/wowdev/WoWDBDefs/master/manifest.json").Result;
            var dbdManifest = Newtonsoft.Json.JsonConvert.DeserializeObject<List<DBDManifestEntry>>(manifest);


            _tableNames = dbdManifest.Select(x => x.tableName).ToList();
        }

        public string GetTableNameForFile(string fileName)
        {
            return _tableNames.FirstOrDefault(x => x.Equals(fileName, StringComparison.CurrentCultureIgnoreCase));
        }

        private struct DBDManifestEntry
        {
            public string tableName;
            public string tableHash;
            public uint dbcFileDataID;
            public uint db2FileDataID;
        }
    }
}
