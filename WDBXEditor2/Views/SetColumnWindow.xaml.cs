using System.Linq;
using System.Windows;
using WDBXEditor2.Core;
using WDBXEditor2.Core.Operations;

namespace WDBXEditor2.Views
{
    public partial class SetColumnWindow : Window
    {
        MainWindow _mainWindow;
        public SetColumnWindow(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            ddlColumnName.ItemsSource = mainWindow.DB2DataGrid.Columns.Select(x => x.Header.ToString()).ToList();
            ddlColumnName.SelectedIndex = 0;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow.RunOperationAsync(new SetColumnOperation()
            {
                Storage = _mainWindow.OpenedDB2Storage,
                ColumnName = ddlColumnName.SelectedValue.ToString(),
                Value = txtValue.Text
            }, true);
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
