using System;
using System.Windows;

namespace WDBXEditor2.Views
{
    public partial class ExceptionWindow : Window
    {
        public Exception CaughtException { get; set; }
        public ExceptionWindow()
        {
            InitializeComponent();
        }

        public void DisplayException(Exception e)
        {
            CaughtException = e;
            TxtException.Text = e.ToString();
        }
    }
}
