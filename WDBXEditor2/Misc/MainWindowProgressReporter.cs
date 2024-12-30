using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
    }
}
