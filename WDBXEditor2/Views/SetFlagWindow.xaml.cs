using System;
using System.Linq;
using System.Windows;
using WDBXEditor2.Core;
using WDBXEditor2.Core.Operations;

namespace WDBXEditor2.Views
{
    public partial class SetFlagWindow : Window
    {
        MainWindow _mainWindow;
        public SetFlagWindow(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            ddlColumnName.ItemsSource = mainWindow.DB2DataGrid.Columns.Select(x => x.Header.ToString()).ToList();
            ddlColumnName.SelectedIndex = 0;
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow.RunOperationAsync(new SetFlagOperation()
            {
                BitValue = uint.Parse(txtValue.Text),
                Unset = cbUnsetBit.IsChecked ?? false,
                ColumnName = ddlColumnName.SelectedValue.ToString(),
                Storage = _mainWindow.OpenedDB2Storage
            }, true);
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
