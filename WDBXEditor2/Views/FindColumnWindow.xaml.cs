using System.Linq;
using System.Windows;
using WDBXEditor2.Core;
using WDBXEditor2.Core.Operations;
using WDBXEditor2.Misc;

namespace WDBXEditor2.Views
{
    public partial class FindColumnWindow : Window
    {
        MainWindow _mainWindow;
        public FindColumnWindow(MainWindow mainWindow)
        {
            InitializeComponent();
            _mainWindow = mainWindow;
            ddlColumnName.ItemsSource = mainWindow.DB2DataGrid.Columns.Select(x => x.Header.ToString()).ToList();
            ddlColumnName.SelectedIndex = 0;
            if (!string.IsNullOrEmpty(mainWindow.SelectedColumnInfo.Name))
            {
                ddlColumnName.SelectedItem = mainWindow.SelectedColumnInfo.Name;

                if (mainWindow.DB2DataGrid.SelectedItem is DBCDRowProxy proxy)
                {
                    txtValue.Text = DBCDHelper.GetDBCRowColumn(proxy.RowData, mainWindow.SelectedColumnInfo.Name).ToString();
                }
            }
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            var selecedFilterType = FilterType.None;
            switch (ddlMatchType.Text)
            {
                case "Contains": selecedFilterType = FilterType.Contains; break;
                case "Exact match": selecedFilterType = FilterType.Exact; break;
                case "RegEx": selecedFilterType = FilterType.RegEx; break;
            }
            _mainWindow.Filter = new Filter()
            {
                Column = ddlColumnName.Text,
                Value = txtValue.Text,
                Type = selecedFilterType,
            };
            _mainWindow.ReloadDataView();
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
