using System;
using WDBXEditor2.Core;

namespace WDBXEditor2.Misc
{
    public class MainWindowProgressReporter : IProgressReporter
    {
        private readonly Lazy<MainWindow> _mainWindow;
        private MainWindow MainWindow => _mainWindow.Value;

        private int _lastReportedProgress = 0;

        public MainWindowProgressReporter(Lazy<MainWindow> mainWindow) 
        { 
            _mainWindow = mainWindow;
        }

        public void ReportProgress(int progressPercentage)
        {
            if (progressPercentage != _lastReportedProgress)
            {
                _lastReportedProgress = progressPercentage;
                MainWindow.Dispatcher.Invoke(() =>
                {
                    MainWindow.ProgressBar.Value = progressPercentage;
                });
            }
        }

        public void SetOperationName(string operationName)
        {
            MainWindow.Dispatcher.Invoke(() =>
            {
                MainWindow.txtOperation.Text = operationName;
            });
        }

        public void SetIsIndeterminate(bool isIndeterminate)
        {
            MainWindow.Dispatcher.Invoke(() =>
            {
                MainWindow.ProgressBar.IsIndeterminate = isIndeterminate;
            });
        }
    }
}
