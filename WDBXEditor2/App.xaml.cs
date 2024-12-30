using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using System.Windows;
using WDBXEditor2.Views;
using WDBXEditor2.Core.Operations;
using WDBXEditor2.Misc;
using DBCD.Providers;
using WDBXEditor2.Operations;
using WDBXEditor2.Core;

namespace WDBXEditor2
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public IServiceProvider ServiceProvider { get; set; }
        

        protected override void OnStartup(StartupEventArgs e)
        {

            var services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();

            SetupExceptionHandling();

            ServiceProvider.GetRequiredService<MainWindow>().Show();
        }

        private void ConfigureServices(ServiceCollection services)
        {
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(typeof(ExportToCsvOperation).Assembly, typeof(ReloadDataViewOperation).Assembly));
            services.AddTransient(typeof(Lazy<>), typeof(LazyResolver<>));
            services.AddSingleton(typeof(MainWindow));
            var settingsStorage = new SettingStorage();
            settingsStorage.Initialize();
            services.AddSingleton<ISettingsStorage>(settingsStorage);
            services.AddTransient<IDBDProvider, GithubDBDProvider>();
            services.AddTransient<IProgressReporter, MainWindowProgressReporter>();
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
                Application.Current.Dispatcher.Invoke(() =>
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
                });
                e.SetObserved();
            };
        }

        internal class LazyResolver<T> : Lazy<T> where T: class
        {
            public LazyResolver(IServiceProvider serviceProvider)
                : base(serviceProvider.GetRequiredService<T>)
            {

            }
        }
    }
}
