using DBCD;
using DBDefsLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WDBXEditor2.Helpers;
using WDBXEditor2.Misc;
using static DBDefsLib.Structs;

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
            var dbcdStorage = _mainWindow.OpenedDB2Storage;
            var columnName = ddlColumnName.SelectedValue.ToString();
            var bitVal = int.Parse(txtValue.Text);
            foreach (var row in dbcdStorage.Values)
            {
                var rowVal = Convert.ToInt32(row[columnName]);
                if (cbUnsetBit.IsChecked ?? false)
                {
                    if ((rowVal & bitVal) > 0)
                    {
                        row[columnName] = ConvertHelper.ConvertValue(row.GetUnderlyingType(), columnName, rowVal - bitVal);
                    }
                } else
                {
                    if ((rowVal & bitVal) == 0)
                    {
                        row[columnName] = ConvertHelper.ConvertValue(row.GetUnderlyingType(), columnName, rowVal + bitVal);
                    }

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
