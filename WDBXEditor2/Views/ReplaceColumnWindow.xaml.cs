using System.Linq;
using System.Windows;
using WDBXEditor2.Core;
using WDBXEditor2.Core.Operations;

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
            _mainWindow.RunOperationAsync(new ReplaceColumnOperation()
            {
                Storage = _mainWindow.OpenedDB2Storage,
                ColumnName = ddlColumnName.SelectedValue.ToString(),
                SearchValue = txtValueReplace.Text,
                ReplacementValue = txtValue.Text
            }, true);
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
