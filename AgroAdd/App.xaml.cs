using AgroAdd.Services;
using AgroAdd.Services.Scrappers;
using AgroAdd.ViewModels;
using AgroAdd.Views;
using System;
using System.Windows;

namespace AgroAdd
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                var loggingService = new LoggingService();
                var currencyApi = new CurrencyApi(loggingService);
                var scrapperProvider = new ScrapperProvider(loggingService, currencyApi); // cia ilgai
                MainWindow = new MainView(loggingService, scrapperProvider);
                MainWindow.Closed += MainWindowClosed;
                MainWindow.Show();
                base.OnStartup(e);
            }
            catch (Exception ex)
            {
                new LoggingService().LogException(ex, "Unhandled exception while starting the app up");
            }
            
        }

        private void MainWindowClosed(object sender, EventArgs e)
        {
            //this.Shutdown();
        }
    }
}
