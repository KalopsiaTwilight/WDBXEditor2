using System.Reflection;

namespace WDBXEditor2
{
    public static class Constants
    {
        public static string Version => Assembly.GetExecutingAssembly().GetName().Version.ToString();

        public const string LastLocaleStorageKey = "LastLocaleSelectedIndex";

        public const string SqlExportTypeStorageKey = "LastSelectedSQLExportTypeIndex";
        public const string LastExportMysqlDbTableKeyPrefix = "LastMysqlDbTable_EXPORT_";
        public const string LastExportMySqlDbHostnameKey = "LastExportMysqlDbHostname";
        public const string LastExportMySqlDbPortKey = "LastExportMysqlDbPort";
        public const string LastExportMySqlDbUserKey= "LastExportMysqlDbUser";
        public const string LastExportMySqlDbPasswordKey = "LastExportMysqlDbPassword";
        public const string LastExportMySqlDbNameKey = "LastExportMysqlDbName";

        public const string SqlImportTypeStorageKey = "LastSelectedSQLImportTypeIndex";
        public const string LastImportMysqlDbTableKeyPrefix = "LastMysqlDbTable_IMPORT_";
        public const string LastImportMySqlDbHostnameKey = "LastImportMysqlDbHostname";
        public const string LastImportMySqlDbPortKey = "LastImportMysqlDbPort";
        public const string LastImportMySqlDbUserKey = "LastImportMysqlDbUser";
        public const string LastImportMySqlDbPasswordKey = "LastImportMySqlDbPassword";
        public const string LastImportMySqlDbNameKey = "LastImportMysqlDbName";

        public const string StoreDbPasswords = "StoreDbPasswords";
    }
}
