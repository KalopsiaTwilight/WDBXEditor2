using System.Linq;
using System.Windows;
using WDBXEditor2.Helpers;

namespace WDBXEditor2.Views
{
    public partial class ReplaceColumnWindow : Window
    {
        MainWindow _mainWindow;
        public ReplaceColumnWindow(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            ddlColumnName.ItemsSource = mainWindow.DB2DataGrid.Columns.Select(x => x.Header.ToString()).ToList();
            ddlColumnName.SelectedIndex = 0;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            var dbcdStorage = _mainWindow.OpenedDB2Storage;
            var columnName = ddlColumnName.SelectedValue.ToString();
            foreach (var row in dbcdStorage.Values)
            {
                if (row[columnName].ToString() == txtValueReplace.Text)
                {
                    row[columnName] = ConvertHelper.ConvertValue(row.GetUnderlyingType(), columnName, txtValue.Text);
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
