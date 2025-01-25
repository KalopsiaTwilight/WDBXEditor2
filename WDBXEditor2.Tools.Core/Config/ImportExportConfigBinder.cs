using System.CommandLine;
using System.CommandLine.Binding;

namespace WDBXEditor2.Tools.Core.Config
{
    public class ImportExportConfigBinder : BinderBase<ImportExportConfig>
    {
        private readonly Argument<string> _inputPath;

        private readonly Option<Mode> _outputFormat;
        private readonly Option<string> _outputPath;
        private readonly Option<bool>? _sqlDropTable;
        private readonly Option<bool>? _sqlCreateTable;
        private readonly Option<bool>? _sqlExportData;
        private readonly Option<uint>? _sqlInsertsPerTable;
        private readonly Option<string> _sqlTableFormatter;
        private readonly Option<string> _mysqlDatabaseHost;
        private readonly Option<uint> _mysqlDatabasePort;
        private readonly Option<string> _mysqlDatabaseName;
        private readonly Option<string> _mysqlDatabaseUsername;
        private readonly Option<string> _mysqlDatabasePassword;
        private readonly Option<string[]> _tables;
        private readonly Option<string> _sqliteDatabaseFile;
        private readonly Option<string> _csvPath;

        public ImportExportConfigBinder(Argument<string> inputPath, Option<Mode> outputFormat,
            Option<string> outputPath, Option<bool> sqlDropTable,
            Option<bool> sqlCreateTable, Option<bool> sqlExportData, Option<uint> sqlInsertsPerTable,
            Option<string> sqlTableFormatter, Option<string> mysqlDatabaseHost, Option<uint> mysqlDatabasePort,
            Option<string> mysqlDatabaseName, Option<string> mysqlDatabaseUsername, Option<string> mysqlDatabasePassword,
            Option<string[]> tables, Option<string> sqliteDatabaseFile, Option<string> csvPath)
        {
            _outputFormat = outputFormat;
            _outputPath = outputPath;
            _inputPath = inputPath;
            _sqlDropTable = sqlDropTable;
            _sqlCreateTable = sqlCreateTable;
            _sqlExportData = sqlExportData;
            _sqlInsertsPerTable = sqlInsertsPerTable;
            _sqlTableFormatter = sqlTableFormatter;
            _mysqlDatabaseHost = mysqlDatabaseHost;
            _mysqlDatabasePort = mysqlDatabasePort;
            _mysqlDatabaseName = mysqlDatabaseName;
            _mysqlDatabaseUsername = mysqlDatabaseUsername;
            _mysqlDatabasePassword = mysqlDatabasePassword;
            _tables = tables;
            _sqliteDatabaseFile = sqliteDatabaseFile;
            _csvPath = csvPath;
        }


        public ImportExportConfigBinder(Argument<string> inputPath, Option<Mode> outputFormat,
            Option<string> outputPath, Option<string> sqlTableFormatter, Option<string> mysqlDatabaseHost, 
            Option<uint> mysqlDatabasePort, Option<string> mysqlDatabaseName, Option<string> mysqlDatabaseUsername, 
            Option<string> mysqlDatabasePassword, Option<string[]> tables, Option<string> sqliteDatabaseFile, 
            Option<string> csvPath)
        {
            _outputFormat = outputFormat;
            _outputPath = outputPath;
            _inputPath = inputPath;
            _sqlTableFormatter = sqlTableFormatter;
            _mysqlDatabaseHost = mysqlDatabaseHost;
            _mysqlDatabasePort = mysqlDatabasePort;
            _mysqlDatabaseName = mysqlDatabaseName;
            _mysqlDatabaseUsername = mysqlDatabaseUsername;
            _mysqlDatabasePassword = mysqlDatabasePassword;
            _tables = tables;
            _sqliteDatabaseFile = sqliteDatabaseFile;
            _csvPath = csvPath;
        }

        protected override ImportExportConfig GetBoundValue(BindingContext bindingContext)
        {
            return new ImportExportConfig()
            {
                Mode = bindingContext.ParseResult.GetValueForOption(_outputFormat),
                OutputPath = bindingContext.ParseResult.GetValueForOption(_outputPath),
                InputPath = bindingContext.ParseResult.GetValueForArgument(_inputPath) ?? string.Empty,
                SqlDropTable = _sqlDropTable != null && bindingContext.ParseResult.GetValueForOption(_sqlDropTable),
                SqlCreateTable = _sqlCreateTable != null && bindingContext.ParseResult.GetValueForOption(_sqlCreateTable),
                SqlExportData = _sqlExportData != null && bindingContext.ParseResult.GetValueForOption(_sqlExportData),
                SqlInsertsPerTable = _sqlInsertsPerTable != null ? bindingContext.ParseResult.GetValueForOption(_sqlInsertsPerTable) : 1000,
                SqlTableFormatter = bindingContext.ParseResult.GetValueForOption(_sqlTableFormatter),
                MySqlDatabaseHost = bindingContext.ParseResult.GetValueForOption(_mysqlDatabaseHost),
                MySqlDatabasePort = bindingContext.ParseResult.GetValueForOption(_mysqlDatabasePort),
                MySqlDatabaseName = bindingContext.ParseResult.GetValueForOption(_mysqlDatabaseName),
                MySqlDatabaseUser = bindingContext.ParseResult.GetValueForOption(_mysqlDatabaseUsername),
                MySqlDatabasePassword = bindingContext.ParseResult.GetValueForOption(_mysqlDatabasePassword),
                Tables = bindingContext.ParseResult.GetValueForOption(_tables) ?? [],
                SqliteDatabaseFile = bindingContext.ParseResult.GetValueForOption(_sqliteDatabaseFile),
                CsvPath = bindingContext.ParseResult?.GetValueForOption(_csvPath),
            };
        }
    }
}
