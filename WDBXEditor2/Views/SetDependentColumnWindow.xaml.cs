using System.Linq;
using System.Windows;
using WDBXEditor2.Core;
using WDBXEditor2.Core.Operations;

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
            _mainWindow.RunOperationAsync(new SetDependentColumnOperation()
            {
                Storage = _mainWindow.OpenedDB2Storage,
                PrimaryColumnName = ddlColumnName.SelectedValue.ToString(),
                PrimaryValue = txtPrimaryValue.Text,
                ForeignColumnName = ddlForeignColumn.SelectedValue.ToString(),
                ForeignValue = txtForeignValue.Text,
            }, true);

            _mainWindow.ReloadDataView();
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
