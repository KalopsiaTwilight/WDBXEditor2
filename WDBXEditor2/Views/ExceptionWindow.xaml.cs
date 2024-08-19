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

        public void DisplayException(Exception e, string label = null)
        {
            CaughtException = e;
            TxtException.Text = e.ToString();
            TxtLabel.Text = label ?? "The application encountered an unexpected error performing this action. For details see below.";
        }
    }
}
