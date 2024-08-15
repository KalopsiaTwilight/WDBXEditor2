using System.Linq;
using System.Windows;
using WDBXEditor2.Helpers;

namespace WDBXEditor2.Views
{
    public partial class SetDependentColumnWindow : Window
    {
        MainWindow _mainWindow;
        public SetDependentColumnWindow(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            ddlColumnName.ItemsSource = mainWindow.DB2DataGrid.Columns.Select(x => x.Header.ToString()).ToList();
            ddlColumnName.SelectedIndex = 0;

            ddlForeignColumn.ItemsSource = mainWindow.DB2DataGrid.Columns.Select(x => x.Header.ToString()).ToList();
            ddlForeignColumn.SelectedIndex = 0;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            var dbcdStorage = _mainWindow.OpenedDB2Storage;
            var columnName = ddlColumnName.SelectedValue.ToString();
            var foreignColumnName = ddlForeignColumn.SelectedValue.ToString();
            foreach (var row in dbcdStorage.Values)
            {
                if (row[columnName].ToString() == txtPrimaryValue.Text)
                {
                    row[foreignColumnName] = ConvertHelper.ConvertValue(row.GetUnderlyingType(), foreignColumnName, txtForeignValue.Text);
                }
            }
            _mainWindow.ReloadDataView();
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
