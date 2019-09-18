using AgroAdd.Interfaces;
using System;
using System.Windows.Forms;
using AgroAdd.Models;
using AgroAdd.Extensions;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AgroAdd.Services.Scrappers
{
    public class MaskinbladetScrapingService : IScrapingService
    {
        private readonly LoggingService _loggingService;
        private readonly CurrencyApi _currencyApi;
        private WebBrowser _scrapBrowser;
        private int? _lastCostMin;
        private int? _lastCostMax;
        private decimal _currentRate = 0.13401m;
        private bool _rateLoaded = false;
        private string _synonyms;
        private string _searchText;
        private bool _isFilteringActive;
        private bool _scrapDone;

        public string ServiceName => "Maskinbladet.dk";
        public string Country => "DK";
        public bool IsAuction => false;
        public bool IsCompany => false;
        public bool RequiresText => true;

        public MaskinbladetScrapingService(LoggingService loggingService, CurrencyApi currencyApi)
        {
            _loggingService = loggingService;
            _currencyApi = currencyApi;
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
                    _scrapBrowser.Navigate($"http://www.maskinbladet.dk/maskiner/landbrugsmaskiner-c1e8ae347-0cba-4690-aa94-e2684c779158?query={query}&rows=50");
                else
                    _scrapBrowser.Navigate($"http://www.maskinbladet.dk/maskiner/landbrugsmaskiner-c1e8ae347-0cba-4690-aa94-e2684c779158?query={query}&rows=50&p={page}");

                if (!_rateLoaded)
                {
                    _currencyApi.GetRateAsync(CurrencyTypes.DanishKrone)
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
                _loggingService.LogException(ex, "Unhandled exception in ScrapMaskinbladetWebBrowserAsync");
            }
        }

        private void ScrapBrowserLoadCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            if (_scrapDone || _scrapBrowser.ReadyState == WebBrowserReadyState.Loading || _scrapBrowser.ReadyState == WebBrowserReadyState.Loaded || _scrapBrowser.ReadyState == WebBrowserReadyState.Uninitialized)
                return;

            IEnumerable<HtmlElement> ads = null;
            string[] filters = null;
            string[] searchTextWords = _searchText.ToLower().Split(' ');
            if (!string.IsNullOrEmpty(_synonyms))
                filters = _synonyms.ToLower().Split(';');
            var results = new List<Advertisement>();
            try
            {
                ads = _scrapBrowser.Document.ElementsByClass("li", "mb-10");
                if (!ads.Any())
                {
                    AsyncScrapCompleted?.Invoke(this, results, false, null);
                    return;
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, "Unhandled exception in ScrapBrowserLoadCompleted");
                AsyncScrapCompleted?.Invoke(this, null, false, "Unhandled exception");
                return;
            }
            try
            {
               
                foreach (HtmlElement add in ads)
                {
                    var title = add.ElementsByClass("h3", "text-18px")?.FirstOrDefault()?.InnerText;
                    bool continueFlag = false;
                    bool breakFlag = false;
                    if (title == null)
                        continue;
                    //Checking if title contains each request text word
                    if (_isFilteringActive && !title.ToLower().Contains(_searchText.ToLower()))
                    {
                        string testTitle = title.ToLower().Replace(" ", "");
                        foreach (var word in searchTextWords)
                        {
                            if (!testTitle.Contains(word))
                            {
                                // Checking if title contains filters
                                if (filters != null && filters.Length != 0)
                                {
                                    foreach (var filter in filters)
                                    {
                                        if (testTitle.Contains(filter) && !filter.Equals(""))
                                            breakFlag = true;
                                    }
                                }
                                if (breakFlag) break;
                                continueFlag = true;
                                break;
                            }
                        }
                    }
                    if (continueFlag) continue;
                    var price = add.ElementsByClass("p", "price")?.FirstOrDefault()?.InnerText;
                    if (!string.IsNullOrWhiteSpace(price))
                    {
                        if (price.ToLower().Contains("ring for pris"))
                            price = "POA";
                        else
                        {
                            price = price.Replace("Pris:","").Replace("DKK","").Replace("-","").Replace(".", "").Replace(",", "").Replace("€", "");
                            if (decimal.TryParse(price, NumberStyles.Any, NumberFormatInfo.InvariantInfo, out decimal decimalPrice))
                            {
                                var euroPrice = (int)(Math.Round(decimalPrice * _currentRate));
                                price = euroPrice.ToString("#,##0") + " €";
                                if (euroPrice <  _lastCostMin)
                                    continue;
                                if (euroPrice >  _lastCostMax)
                                    continue;
                            }
                        }

                    }
                    var href = SafeExtractHref(add);
                    var description = add.ElementsByClass("p", "description")?.FirstOrDefault()?.InnerText+ Environment.NewLine + add.ElementsByClass("p", "location-info")?.FirstOrDefault()?.InnerText;
                    var moreDescription = add.ElementsByClass("li", "year")?.FirstOrDefault()?.InnerText;
                    var moremoreDescription = add.ElementsByClass("li", "hours")?.FirstOrDefault()?.InnerText;
                    var moremoremoreDescription = add.ElementsByClass("li", "hp")?.FirstOrDefault()?.InnerText;

                    description ="Year: " + moreDescription + " Hours: " + moremoreDescription + " HP: " + moremoremoreDescription + Environment.NewLine + description;
                    var srcDiv = add.ElementsByClass("figure", "image")?.FirstOrDefault();
                    var imgSrc = SafeExtractSrc(srcDiv);
                    if (imgSrc == null || imgSrc == " " || imgSrc == "")
                        imgSrc = "Images/noimage.png";

                    results.Add(new Advertisement
                    {
                        Name = title,
                        Description = description,
                        Price = price,
                        ImageUrl = imgSrc,
                        PageUrl = href,
                    });
                }
                var hasMorePages = ScrapHasMorePages(_scrapBrowser.Document);
                _scrapDone = true;
                _scrapBrowser.Stop();
                AsyncScrapCompleted?.Invoke(this, results, hasMorePages, null);
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, "Unhandled exception in ScrapBrowserLoadCompleted");
                AsyncScrapCompleted?.Invoke(this, null, false, "Unhandled exception");
            }
            /*finally
            {
                if (_scrapBrowser != null)
                {
                    _scrapBrowser.Dispose();
                    _scrapBrowser = null;
                }
            }*/

        }
        private bool ScrapHasMorePages(HtmlDocument document)
        {
            try
            {
                var pagination = document.ElementsByClass("ul", "pagination")?.FirstOrDefault();
                return HasNextPageIndicator(pagination);
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, "Failed to parse pagination");
                return false;
            }
        }

        private bool HasNextPageIndicator(HtmlElement paginationElement)
        {
            for (int i = 0; i < paginationElement?.GetElementsByTagName("a").Count; i++)
            {
                var rel = paginationElement?.GetElementsByTagName("a")[i]?.InnerText;
                if (rel.Contains("»"))
                {
                    return true;
                }
            }
            return false;
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
        private T ElementAtOrDefault<T>(IEnumerable<T> collection, int index)
        {
            if (collection == null || collection.Count() <= index)
                return default(T);
            return collection.ElementAt(index);
        }
    }
}
