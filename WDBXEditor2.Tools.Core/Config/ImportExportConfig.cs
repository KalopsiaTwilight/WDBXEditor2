namespace WDBXEditor2.Tools.Core.Config
{
    public enum Mode
    {
        Csv,
        Mysql,
        Sqlite
    }

    public class ImportExportConfig
    {
        public Mode Mode { get; set; } = Mode.Csv;
        public string? OutputPath { get; set; }
        public string InputPath { get; set; } = string.Empty;
        public string[] Tables { get; set; } = [];

        public string? CsvPath { get; set; }

        public bool SqlDropTable { get; set; } = true;
        public bool SqlCreateTable { get; set; } = true;
        public bool SqlExportData { get; set; } = true;
        public uint SqlInsertsPerTable { get; set; } = 1000;
        public string? SqlTableFormatter { get; set; }

        public string? MySqlDatabaseHost { get; set; } = "localhost";
        public uint MySqlDatabasePort { get; set; } = 3306;
        public string? MySqlDatabaseName { get; set; }
        public string? MySqlDatabaseUser { get; set; }
        public string? MySqlDatabasePassword { get; set; }

        public string? SqliteDatabaseFile { get; set; }

        public void Merge(ImportExportConfig other)
        {
            var def = new ImportExportConfig();

            var props = typeof(ImportExportConfig).GetProperties();
            foreach (var prop in props)
            {
                var defVal = prop.GetValue(def);
                var otherVal = prop.GetValue(other);
                if (defVal != null && otherVal != null)
                {
                    if (!otherVal.Equals(defVal))
                    {
                        prop.SetValue(this, prop.GetValue(other));
                    }
                } else if (defVal == null && otherVal != null)
                {
                    prop.SetValue(this, otherVal);
                }
            }
        }
    }
}
