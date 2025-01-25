using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WDBXEditor2.Core;

namespace WDBXEditor2.Tools.Core
{
    public class ConsoleProgressReporter : IProgressReporter, IDisposable
    {
        private readonly IConsole _console;
        private readonly ProgressBar _progressBar;

        public ConsoleProgressReporter(IConsole console)
        {
            _console = console;
            _progressBar = new ProgressBar(console);
        }

        public void Dispose()
        {
            _progressBar.Dispose();
        }

        public void ReportProgress(int progressPercentage)
        {
            if (progressPercentage != 100)
            {
                _progressBar.Report(progressPercentage);
            } else
            {
                _progressBar.Reset();
            }
        }

        public void SetIsIndeterminate(bool isIndeterminate)
        {
        }

        public void SetOperationName(string operationName)
        {
            _progressBar.Reset();
            _console.WriteLine(operationName);
        }
    }
}
