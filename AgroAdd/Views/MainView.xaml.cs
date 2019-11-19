using AgroAdd.Services;
using AgroAdd.Services.Scrappers;
using AgroAdd.ViewModels;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AgroAdd.Views
{
    public partial class MainView : Window
    {
        private readonly LoggingService _loggingService;

        public MainView(LoggingService loggingService, ScrapperProvider scrapperProvider)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            _loggingService = loggingService;
            InitializeComponent();
            DataContext = new MainViewViewModel(loggingService, scrapperProvider);
            (DataContext as MainViewViewModel).PropertyChanged += MainViewPropertyChanged;
            KeyUp += OnWindowKeyUp;
            ((MainViewViewModel)DataContext).UpdateFocus += OnUpdateFocus;
        }

        private void OnUpdateFocus(object sender)
        {
            AdsList.Focus();
        }

        private void OnWindowKeyUp(object sender, KeyEventArgs e)
        {
            var context = DataContext as MainViewViewModel;
            if (context == null)
                return;
            if(e.Key == Key.Enter && context.SearchCommand.CanExecute(1))
            {
                AdsList.Focus();
                context.SearchCommand.Execute(1);
                return;
            }

            if (e.Key == Key.Left && 
                context.PreviousSubPageCommand.CanExecute(1) && 
                !searchTextBox.IsFocused &&
                !searchFilterTextBox.IsFocused)
            {
                context.PreviousSubPageCommand.Execute(1);
                return;
            }
            if (e.Key == Key.Right && 
                context.NextSubPageCommand.CanExecute(1) &&
                !searchTextBox.IsFocused &&
                !searchFilterTextBox.IsFocused)
            {
                context.NextSubPageCommand.Execute(1);
                return;
            }
            if (e.Key == Key.NumPad1 && 
                context.SwitchPageCommand.CanExecute(-1) &&
                !searchTextBox.IsFocused &&
                !searchFilterTextBox.IsFocused)
            {
                context.SwitchPageCommand.Execute(-1);
                return;
            }
            if (e.Key == Key.NumPad3 && 
                context.SwitchPageCommand.CanExecute(1) &&
                !searchTextBox.IsFocused &&
                !searchFilterTextBox.IsFocused)
            {
                context.SwitchPageCommand.Execute(1);
                return;
            }
            
        }

        private void MainViewPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != "SubPageIndex" && e.PropertyName != "PageIndex")
                return;

            try
            {
                (((VisualTreeHelper.GetChild(AdsList, 0) as Border).Child) as ScrollViewer).ScrollToVerticalOffset(0);
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, "Unhandled exception in ScrapEuropeAgrocultureWebBrowserAsync");
            }
            
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private void OnXClickUpEvent(object sender, MouseButtonEventArgs e)
        {
            var context = DataContext as MainViewViewModel;
            if (context == null)
                return;
            if (context.UntoggleAllCommandCanExecute(1))
                context.UntoggleAllComandExecute(1);
        }

        private void OnXMouseEnterEvent(object sender, MouseEventArgs e)
        {
            XBlock.SetCurrentValue(ForegroundProperty,Brushes.White);
        }

        private void OnXMouseEnterLeave(object sender, MouseEventArgs e)
        {
            XBlock.SetCurrentValue(ForegroundProperty, Brushes.IndianRed);
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var context = DataContext as MainViewViewModel;
            if (context == null)
                return;
            if (context.IsTheCheapestSelected)
                context.SortAscending();
            else if (context.IsTheMostExpensiveSelected)
                context.SortDescending();
            AdsList.Focus();
        }
    }
}
