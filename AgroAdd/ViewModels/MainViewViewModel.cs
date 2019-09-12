using AgroAdd.Interfaces;
using AgroAdd.Models;
using AgroAdd.Models.Mvvm;
using AgroAdd.Services;
using AgroAdd.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Diagnostics;
using System.Windows.Navigation;
using System.Windows.Controls;
using System.Timers;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows;

namespace AgroAdd.ViewModels
{
    public class MainViewViewModel : BaseViewModel
    {
        private readonly LoggingService _loggingService;
        private readonly ScrapperProvider _scrapperProvider;
        private readonly ObservableCollection<ScrapperInfoViewModel> _scrappers;
        private ObservableCollection<AdvertisementViewModel> _advertisements;
        private IComparer<AdvertisementViewModel> _comparer;
        private Random _random = new Random();
        private DispatcherTimer aTimer;
        private bool _isSearching;
        private bool _isRefreshing;
        private bool _isTheCheapestSelected;
        private bool _isTheMostExpensiveSelected;
        private bool _isFilteringChecked = true;
        private string _searchText;
        private string _searchFilterText;
        private int? _scrapperProgressBarValue;
        private int? _checkedScrappersCount;
        private double _memoryProgressBarValue;
        private int? _memoryProgressBarMax;
        private int _pageIndex = 1;
        private int? _costMin = 5000;
        private int? _costMax = null;
        private int _subPageIndex = 1;
        private int _subPageSize = 30;
        private Visibility _filterRequiredVisibility;

        private List<AdvertisementViewModel> _unpagedAdvertisements { get; set; }

        public MainViewViewModel(LoggingService loggingService, ScrapperProvider scrapperProvider)
        {
            _loggingService = loggingService;
            _scrapperProvider = scrapperProvider;
            _scrappers = new ObservableCollection<ScrapperInfoViewModel>(scrapperProvider.ScrapingServices.Select(x => new ScrapperInfoViewModel(x)));
            OnPropertyChanged(nameof(Scrappers));
            foreach (var scrapper in _scrappers)
            {
                scrapper.Model.AsyncScrapCompleted += ScrapingCompleted;
                scrapper.IsCheckedChanged += () =>
                {
                    OnPropertyChanged(nameof(IsAllChecked));
                    OnPropertyChanged(nameof(IsAuctionsChecked));
                    OnPropertyChanged(nameof(IsAdsChecked));
                    OnPropertyChanged(nameof(IsCompaniesChecked));
                };
            }

            _loggingService.LogText("View Model started");
            CanSearch = true;
            FilterRequiredVisibility = Visibility.Collapsed;

            _advertisements = new ObservableCollection<AdvertisementViewModel>();
            _unpagedAdvertisements = new List<AdvertisementViewModel>();
            _comparer = new AdvertisementsComparer();
            SearchCommand = new DelegateCommand(SearchCommandExecute, SearchCommandCanExecute);
            SwitchPageCommand = new DelegateCommand(SwitchPageCommandExecute, SwitchPageCommandCanExecute);
            RefreshCommand = new DelegateCommand(RefreshCommandExecute, RefreshCommandCanExecute);
            ToggleAllSearchesCommand = new DelegateCommand(ToggleAllSearchesCommandExecute, ToggleAllSearchesCommandCanExecute);
            ToggleAllAuctionsCommand = new DelegateCommand(ToggleAllAuctionsCommandExecute, ToggleAllAcutionsCommandCanExecute);
            ToggleAllAdsCommand = new DelegateCommand(ToggleAllAdsCommandExecute, ToggleAllAdsCommandCanExecute);
            ToggleAllCompaniesCommand = new DelegateCommand(ToggleAllCompaniesCommandExecute, ToggleAllCompaniesCommandCanExecute);
            UntoggleAllCommand = new DelegateCommand(UntoggleAllComandExecute, UntoggleAllCommandCanExecute);
            PreviousSubPageCommand = new DelegateCommand(PreviousSubPageCommandExecute, PreviousSubPageCommandCanExecute);
            NextSubPageCommand = new DelegateCommand(NextSubPageCommandExecute, NextSubPageCommandCanExecute);

            SetTimer();
        }

        public ObservableCollection<AdvertisementViewModel> Advertisements
        {
            get => _advertisements;
            set
            {
                _advertisements = value;
                OnPropertyChanged(nameof(Advertisements), SwitchPageCommand);
            }
        }
        public ObservableCollection<ScrapperInfoViewModel> Scrappers { get => _scrappers; }
        public bool IsAllChecked => _scrappers?.All(x => x.IsChecked) ?? false;
        public bool IsAuctionsChecked => _scrappers?.Where(x => x.IsAuction && !x.IsCompany)?.All(x => x.IsChecked) ?? false;
        public bool IsAdsChecked => _scrappers?.Where(x => !x.IsAuction && !x.IsCompany)?.All(x => x.IsChecked) ?? false;
        public bool IsCompaniesChecked => _scrappers?.Where(x => x.IsCompany && !x.IsAuction)?.All(x => x.IsChecked) ?? false;
        public bool IsTheCheapestSelected
        {
            get => _isTheCheapestSelected;
            set
            {
                _isTheCheapestSelected = value;
                OnPropertyChanged(nameof(IsTheCheapestSelected));
            }
        }
        public bool IsTheMostExpensiveSelected
        {
            get => _isTheMostExpensiveSelected;
            set
            {
                _isTheMostExpensiveSelected = value;
                OnPropertyChanged(nameof(IsTheMostExpensiveSelected));
            }
        }
        public bool IsFilteringChecked
        {
            get => _isFilteringChecked;
            set
            {
                _isFilteringChecked = value;
                OnPropertyChanged(nameof(IsFilteringChecked));
            }
        }
        public bool CanSearch
        {
            get => _isSearching;
            set
            {
                _isSearching = value;
                OnPropertyChanged(nameof(CanSearch));
            }
        }
        public bool CanRefresh
        {
            get => _isRefreshing;
            set
            {
                _isRefreshing = value;
                OnPropertyChanged(nameof(CanRefresh));
            }
        }
        public int PageIndex
        {
            get => _pageIndex;
            set
            {
                _pageIndex = value;
                OnPropertyChanged(nameof(PageIndex), SwitchPageCommand);
            }
        }
        public int SubPageIndex
        {
            get => _subPageIndex;
            set
            {
                _subPageIndex = value;
                OnPropertyChanged(nameof(SubPageIndex));
            }
        }
        public int TotalSubPages => (int)Math.Ceiling((_unpagedAdvertisements.Count() + 1) / (decimal)_subPageSize);
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged(nameof(SearchText), SwitchPageCommand);
            }
        }
        public string SearchFilterText
        {
            get => _searchFilterText;
            set
            {
                _searchFilterText = value;
                OnPropertyChanged(nameof(SearchFilterText), SwitchPageCommand);
            }
        }
        public int? CostMin
        {
            get => _costMin;
            set
            {
                _costMin = value;
                OnPropertyChanged(nameof(CostMin));
            }
        }
        public int? CostMax
        {
            get => _costMax;
            set
            {
                _costMax = value;
                OnPropertyChanged(nameof(CostMax));
            }
        }
        public int? ScrapperProgressBarValue
        {
            get => _scrapperProgressBarValue;
            set
            {
                _scrapperProgressBarValue = value;
                OnPropertyChanged(nameof(ScrapperProgressBarValue));
            }
        }
        public int? CheckedScrappersCount
        {
            get => _checkedScrappersCount;
            set
            {
                _checkedScrappersCount = value;
                OnPropertyChanged(nameof(CheckedScrappersCount));
            }
        }
        public double MemoryProgressBarValue
        {
            get => _memoryProgressBarValue;
            set
            {
                _memoryProgressBarValue = value;
                OnPropertyChanged(nameof(MemoryProgressBarValue));
            }
        }
        public int? MemoryProgressBarMax
        {
            get => _memoryProgressBarMax;
            set
            {
                _memoryProgressBarMax = value;
                OnPropertyChanged(nameof(MemoryProgressBarMax));
            }
        }

        public Visibility FilterRequiredVisibility
        {
            get => _filterRequiredVisibility;
            set
            {
                _filterRequiredVisibility = value;
                OnPropertyChanged(nameof(FilterRequiredVisibility));
            }
        }
        public DelegateCommand SearchCommand { get; private set; }
        public DelegateCommand SwitchPageCommand { get; private set; }
        public DelegateCommand RefreshCommand { get; private set; }
        public DelegateCommand ToggleAllSearchesCommand { get; private set; }
        public DelegateCommand ToggleAllAuctionsCommand { get; private set; }
        public DelegateCommand ToggleAllAdsCommand { get; private set; }
        public DelegateCommand ToggleAllCompaniesCommand { get; private set; }
        public DelegateCommand UntoggleAllCommand { get; private set; }
        public DelegateCommand GoToUrlCommand { get; private set; }
        public DelegateCommand PreviousSubPageCommand { get; private set; }
        public DelegateCommand NextSubPageCommand { get; private set; }

        public delegate void UpdateFocusAction(MainViewViewModel model);
        public event UpdateFocusAction UpdateFocus;

        public bool SearchCommandCanExecute(object commandParameter)
        {
            return true;
        }
        public void SearchCommandExecute(object commandParameter)
        {
            if (_scrappers.All(x => !x.IsChecked))
                return;

            if (string.IsNullOrWhiteSpace(SearchText) && _scrappers.Where(x => x.IsChecked).Any(x => x.RequiresText))
            {
                FilterRequiredVisibility = Visibility.Visible;
                return;
            }

            else
            {
                FilterRequiredVisibility = Visibility.Collapsed;
            }

            ScrapperProgressBarValue = null;
            CheckedScrappersCount = null;
            _pageIndex = 1;
            PageIndex = 1;
            SubPageIndex = 1;
            UpdateFocus?.Invoke(this);
            UpdatePage();
            _unpagedAdvertisements.Clear();
            _advertisements.Clear();
            foreach (var scrapper in _scrappers)
            {   
                // clear status of unchecked scrappers
                if (!scrapper.IsChecked)
                {
                    scrapper.MorePagesIndicator = "";
                    scrapper.IsFinished = false;
                    scrapper.SearchResults = "";
                }
                // execute search
                if (scrapper.IsChecked)
                {
                    if (CheckedScrappersCount != null)
                        CheckedScrappersCount++;
                    else
                        CheckedScrappersCount = 1;
                    scrapper.IsFinished = false;
                    scrapper.SearchResults = "...";
                    scrapper.Model.ScrapAsync(SearchText, SearchFilterText, IsFilteringChecked, CostMin, CostMax, PageIndex);
                }
            }
        }

        public bool SwitchPageCommandCanExecute(object commandParameter)
        {
            return true;

        }
        public void SwitchPageCommandExecute(object commandParameter)
        {
            if (!int.TryParse(commandParameter.ToString(), out int pageIndex))
                return;
            if (_pageIndex + pageIndex < 1)
                return;

            if (string.IsNullOrWhiteSpace(SearchText) && _scrappers.Where(x => x.IsChecked).Any(x => x.RequiresText))
            {
                FilterRequiredVisibility = Visibility.Visible;
                return;
            }

            else
            {
                FilterRequiredVisibility = Visibility.Collapsed;
            }

            ScrapperProgressBarValue = 0;
            CheckedScrappersCount = 0;
            SubPageIndex = 1;
            UpdateFocus?.Invoke(this);
            UpdatePage();
            PageIndex += pageIndex;
            _unpagedAdvertisements.Clear();
            _advertisements.Clear();
            foreach (var scrapper in _scrappers)
            {
                // clear status of unchecked scrappers
                if (!scrapper.IsChecked)
                {
                    scrapper.MorePagesIndicator = "";
                    scrapper.IsFinished = false;
                    scrapper.SearchResults = "";
                }

                // clear status for checked scrappers and scrap if can switch page
                if (scrapper.IsChecked)
                {
                    scrapper.IsFinished = false;
                    scrapper.SearchResults = "";
                    // finding last page
                    if (scrapper.MorePagesIndicator == "" && pageIndex == 1 && !scrapper.HasLastPage)
                    {
                        scrapper.LastPage = PageIndex - 1;
                        scrapper.HasLastPage = true;
                    }
                    // switching pages forward
                    else if (scrapper.HasMorePagesIndicator || scrapper.LastPage >= PageIndex)
                    {
                        CheckedScrappersCount++;
                        scrapper.SearchResults = "...";
                        scrapper.Model.ScrapAsync(SearchText, SearchFilterText, IsFilteringChecked, CostMin, CostMax, PageIndex);
                    }
                }
            }
        }

        public bool RefreshCommandCanExecute(object commandParameter)
        {
            return true;

        }
        public void RefreshCommandExecute(object commandParameter)
        {
            UpdateFocus?.Invoke(this);
            foreach (var scrapper in _scrappers)
            {
                if (!scrapper.IsFinished && scrapper.IsChecked)
                {
                    scrapper.IsFinished = false;
                    scrapper.SearchResults = "rr";
                    scrapper.Model.ScrapAsync(SearchText, SearchFilterText, IsFilteringChecked, CostMin, CostMax, PageIndex);
                }
            }
        }

        public bool ToggleAllSearchesCommandCanExecute(object commandParameter)
        {
            return true;
        }
        public void ToggleAllSearchesCommandExecute(object commandParameter)
        {
            UpdateFocus?.Invoke(this);
            if (_scrappers == null || !_scrappers.Any())
                return;
            var isAllChecked = _scrappers.All(x => x.IsChecked);


            foreach (var scrapper in _scrappers)
                    scrapper.IsChecked = !isAllChecked;

        }

        public bool ToggleAllAcutionsCommandCanExecute(object commandParameter)
        {
            return true;
        }
        public void ToggleAllAuctionsCommandExecute(object commandParameter)
        {
            UpdateFocus?.Invoke(this);
            if (_scrappers == null || !_scrappers.Any())
                return;
            var isAllChecked = _scrappers.Where(x => x.IsAuction && !x.IsCompany).All(x => x.IsChecked);
            foreach (var scrapper in _scrappers.Where(x => x.IsAuction && !x.IsCompany))
                scrapper.IsChecked = !isAllChecked;

        }

        public bool ToggleAllAdsCommandCanExecute(object commandParameter)
        {
            return true;
        }
        public void ToggleAllAdsCommandExecute(object commandParameter)
        {
            UpdateFocus?.Invoke(this);
            if (_scrappers == null || !_scrappers.Any())
                return;
            var isAllChecked = _scrappers.Where(x => !x.IsAuction && !x.IsCompany).All(x => x.IsChecked);
            foreach (var scrapper in _scrappers.Where(x => !x.IsAuction && !x.IsCompany))
                scrapper.IsChecked = !isAllChecked;

        }

        public bool ToggleAllCompaniesCommandCanExecute(object commandParameter)
        {
            return true;
        }
        public void ToggleAllCompaniesCommandExecute(object commandParameter)
        {
            UpdateFocus?.Invoke(this);
            if (_scrappers == null || !_scrappers.Any())
                return;
            var isAllChecked = _scrappers.Where(x => !x.IsAuction && x.IsCompany).All(x => x.IsChecked);
            foreach (var scrapper in _scrappers.Where(x => !x.IsAuction && x.IsCompany))
                scrapper.IsChecked = !isAllChecked;

        }

        public bool UntoggleAllCommandCanExecute(object commandParameter)
        {
            return true;
        }
        public void UntoggleAllComandExecute(object commandParameter)
        {
            UpdateFocus?.Invoke(this);
            if (_scrappers == null || !_scrappers.Any())
                return;
            foreach (var scrapper in _scrappers)
                scrapper.IsChecked = false;
        }

        public bool PreviousSubPageCommandCanExecute(object commandParameter)
        {
            return true;
        }
        public void PreviousSubPageCommandExecute(object commandParameter)
        {
            UpdateFocus?.Invoke(this);
            if (_unpagedAdvertisements == null || !_unpagedAdvertisements.Any())
                return;
            if (_subPageIndex > 1)
                SubPageIndex--;
            UpdatePage();
        }

        public bool NextSubPageCommandCanExecute(object commandParameter)
        {
            return true;
        }
        public void NextSubPageCommandExecute(object commandParameter)
        {
            UpdateFocus?.Invoke(this);
            if (_unpagedAdvertisements == null || !_unpagedAdvertisements.Any())
                return;
            if (_subPageIndex < TotalSubPages)
                SubPageIndex++;
            UpdatePage();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }
        private void ScrapingCompleted(IScrapingService sender, IEnumerable<Advertisement> results, bool hasMorePages, string error)
        {
            var finishedScrapper = Scrappers.FirstOrDefault(x => x.Name == sender.ServiceName);
            if (finishedScrapper != null)
            {
                if (ScrapperProgressBarValue != null)
                    ScrapperProgressBarValue++;
                else
                    ScrapperProgressBarValue = 1;
                finishedScrapper.IsFinished = true;
                if (error != null)
                {
                    finishedScrapper.SearchResults = "ER";
                }
                else
                {
                    finishedScrapper.IsFinished = true;
                    finishedScrapper.SearchResults = (results?.Count() ?? 0).ToString();
                    finishedScrapper.MorePagesIndicator = hasMorePages ? "+" : "";
                    if (finishedScrapper.MorePagesIndicator == "+")
                        finishedScrapper.HasMorePagesIndicator = true;
                    else
                        finishedScrapper.HasMorePagesIndicator = false;
                }
            }
            CanRefresh = true;
            CanSearch = true;
            if (results == null)
                return;
            _unpagedAdvertisements.AddRange(results.Select(x => new AdvertisementViewModel(x)));
            if (IsTheCheapestSelected)
                _unpagedAdvertisements = _unpagedAdvertisements.OrderBy(x => x, _comparer).ToList();
            if (IsTheMostExpensiveSelected)
                _unpagedAdvertisements = _unpagedAdvertisements.OrderByDescending(x => x, _comparer).ToList();

            UpdatePage();
        }

        public void SortAscending()
        {
            _unpagedAdvertisements = _unpagedAdvertisements.OrderBy(x => x, _comparer).ToList();
            UpdatePage();
        }

        public void SortDescending()
        {
            _unpagedAdvertisements = _unpagedAdvertisements.OrderByDescending(x => x, _comparer).ToList();
            UpdatePage();
        }

        private void UpdatePage()
        {
            Advertisements.Clear();
            foreach (var adv in _unpagedAdvertisements.Skip(_subPageSize * (_subPageIndex - 1)).Take(_subPageSize))
                Advertisements.Add(adv);
            OnPropertyChanged(nameof(TotalSubPages));
        }

        public void SetTimer()
        {
            aTimer = new DispatcherTimer();
            aTimer.Tick += OnTimedEvent;
            aTimer.Interval = TimeSpan.FromMilliseconds(500);
            aTimer.Start();
        }

        private void OnTimedEvent(object sender, EventArgs e)
        {
            //Task.Run(() =>{
            using (var proc = Process.GetCurrentProcess())
            {
                MemoryProgressBarValue = Math.Round(proc.PrivateMemorySize64 / 1e+6, 2);
            }
            //});

        }
    }
}
