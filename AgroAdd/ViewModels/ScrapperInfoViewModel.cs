using AgroAdd.Interfaces;
using AgroAdd.Models.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgroAdd.ViewModels
{
    public class ScrapperInfoViewModel : BaseViewModel
    {
        private bool _isChecked;
        private bool _isFinished;
        private string _searchResults;
        private string _morePagesIndicator;
        private int _lastPage;
        private bool _hasLastPage;
        private bool _hasMorePagesIndicator;

        public ScrapperInfoViewModel(IScrapingService model)
        {
            Model = model;
        }

        public delegate void IsCheckedChangeDelegate();
        public event IsCheckedChangeDelegate IsCheckedChanged;
        public IScrapingService Model { get; private set; }

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                _isChecked = value;
                IsFinished = false;
                SearchResults = "";
                MorePagesIndicator = "";
                IsCheckedChanged?.Invoke();
                OnPropertyChanged(nameof(IsChecked));
            }
        }
        public bool IsAuction { get => Model.IsAuction; }
        public bool IsCompany { get => Model.IsCompany; }
        public bool RequiresText { get => Model.RequiresText; }
        public bool IsFinished
        {
            get => _isFinished;
            set
            {
                _isFinished = value;
                OnPropertyChanged(nameof(IsFinished));
            }
        }
        public string SearchResults
        {
            get => _searchResults;
            set
            {
                _searchResults = value;
                OnPropertyChanged(nameof(SearchResults));
            }
        }
        public int LastPage
        {
            get => _lastPage;
            set
            {
                _lastPage = value;
                OnPropertyChanged(nameof(LastPage));
            }
        }
        public bool HasLastPage
        {
            get => _hasLastPage;
            set
            {
                _hasLastPage = value;
                OnPropertyChanged(nameof(HasLastPage));
            }
        }
        public string Name { get => Model.ServiceName; }
        public string Country { get => Model.Country; }
        public string MorePagesIndicator
        {
            get => _morePagesIndicator; set
            {
                _morePagesIndicator = value;
                OnPropertyChanged(nameof(MorePagesIndicator));
            }
        }
        public bool HasMorePagesIndicator
        {
            get => _hasMorePagesIndicator; set
            {
                _hasMorePagesIndicator = value;
                OnPropertyChanged(nameof(HasMorePagesIndicator));
            }
        }

        public override string ToString()
        {
            return Name; //$"{Name} ({Country})";
        }

    }
}
