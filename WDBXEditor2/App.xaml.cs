using System;
using System.Threading.Tasks;
using System.Windows;
using WDBXEditor2.Views;

namespace WDBXEditor2
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            SetupExceptionHandling();
        }

        private void SetupExceptionHandling()
        {
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var exceptionWindow = new ExceptionWindow();
                exceptionWindow.DisplayException((Exception)e.ExceptionObject);
                exceptionWindow.Show();
            };
            DispatcherUnhandledException += (s, e) =>
            {
                var exceptionWindow = new ExceptionWindow();
                exceptionWindow.DisplayException(e.Exception);
                exceptionWindow.Show();
                e.Handled = true;
            };
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                var exceptionWindow = new ExceptionWindow();
                if (e.Exception.InnerException != null)
                {
                    exceptionWindow.DisplayException(e.Exception.InnerException);
                }
                else
                {
                    exceptionWindow.DisplayException(e.Exception);
                }
                exceptionWindow.Show();
                e.SetObserved();
            };
        }
    }
}
