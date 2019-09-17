using AgroAdd.Interfaces;
using System;
using System.Windows.Forms;
using AgroAdd.Models;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Newtonsoft.Json;
using System.Windows.Threading;
using AgroAdd.Services.Scrappers.TradeMachines.Models;

namespace AgroAdd.Services.Scrappers.TradeMachinesScrapper
{
    public class TradeMachinesScrapingService : IScrapingService
    {
        private readonly LoggingService _loggingService;
        private readonly CurrencyApi _currencyApi;
        private WebBrowser _scrapBrowser;
        private int? _lastCostMin;
        private int? _lastCostMax;
        private decimal _currentRate = 0.88245m;
        private bool _rateLoaded = false;
        private string _synonyms;
        private string _searchText;
        private bool _isFilteringActive;
        private bool _scrapDone;

        public string ServiceName => "Trademachines.fr";
        public string Country => "FR";
        public bool IsAuction => false;
        public bool IsCompany => false;
        public bool RequiresText => true;

        public TradeMachinesScrapingService(LoggingService loggingService)
        {
            _loggingService = loggingService;
        }

        public event ScrapCompleted AsyncScrapCompleted;

        public void ScrapAsync(string query, string synonyms, bool filtering, int? costmin, int? costmax, int page = 1)
        {
            _lastCostMin = costmin;
            _lastCostMax = costmax;
            _synonyms = synonyms;
            _scrapDone = false;
            _searchText = query;
            _isFilteringActive = filtering;
            if (_scrapBrowser == null)
            {
                _scrapBrowser = new WebBrowser();
                _scrapBrowser.DocumentCompleted += ScrapBrowserLoadCompleted;
                _scrapBrowser.ScriptErrorsSuppressed = true;
            }
            try
            {
                if (page < 2)
                    _scrapBrowser.Navigate($"https://trademachines.fr/search?phrase={query}");
                else
                    _scrapBrowser.Navigate($"https://trademachines.fr/search?phrase={query}&page={page}");

                if (!_rateLoaded)
                {
                    _currencyApi.GetRateAsync(CurrencyTypes.UnitedStatesDollar)
                        .ContinueWith((rateTask) =>
                        {
                            _rateLoaded = true;
                            if (rateTask.IsFaulted || rateTask.IsCanceled)
                                return;
                            if (rateTask.Result > 0)
                                _currentRate = rateTask.Result;
                        });
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, "Unhandled exception in ScrapTradeMachinesWebBrowserAsync");
            }
        }

        private void ScrapBrowserLoadCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            try
            {
                string apiResponse = _scrapBrowser.Document.Body.InnerHtml;
                var start = apiResponse.IndexOf("{");
                var end = apiResponse.LastIndexOf("}");
                apiResponse = apiResponse.Substring(start, end-start+1).Trim();

                var results = new List<Advertisement>();
                var parsedResponse = JsonConvert.DeserializeObject<TradeMachinesSearchResponse>(apiResponse);

                if (parsedResponse == null || !parsedResponse.props.pageProps.resultsState._originalResponse.results.hits.Any())
                {
                    AsyncScrapCompleted?.Invoke(this, results, false, null);
                }

                foreach (var add in parsedResponse.props.pageProps.resultsState._originalResponse.results.hits)
                {
                    string img;
                    if (add.product.isSparePart == true) continue;
                    if (add.hasImg)
                        img = "dizv3061bgivy.cloudfront.net/images" + add.imgId;
                    else
                        img = "Images/noimage.png";
                    results.Add(new Advertisement
                    {
                        Name = add.product.name,
                        Description = "Year: " + add.year + 
                        Environment.NewLine + add.location.country + " " + add.location.state + " " + add.location.city,
                        Price = add.price.ToString(),
                        ImageUrl = img,
                        PageUrl = "https://trademachines.fr/lots/" + add.objectID,
                    }) ;
                }
                    AsyncScrapCompleted?.Invoke(this, results, false, null);
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, "Unhandled exception in ScrapBrowserLoadCompleted");
                 AsyncScrapCompleted?.Invoke(this, null, false, "Unhandled exception");
            }

        }
        private string SafeExtractHref(HtmlElement simpleAdd)
        {
            var el = simpleAdd?.GetElementsByTagName("a");
            if (el == null)
                return null;
            if (el.Count == 0)
                return null;
            return el[0].GetAttribute("href");
        }
        private string SafeExtractSrc(HtmlElement simpleAdd)
        {
            var el = simpleAdd?.GetElementsByTagName("img");
            if (el == null)
                return null;
            if (el.Count == 0)
                return null;
            return el[0].GetAttribute("src");
        }
    }
}
