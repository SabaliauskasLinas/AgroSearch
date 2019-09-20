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
    public class TrattoriSupermarketScrapingService : IScrapingService
    {
        private readonly LoggingService _loggingService;
        private WebBrowser _scrapBrowser;
        private int? _lastCostMin;
        private int? _lastCostMax;
        private string _synonyms;
        private string _searchText;
        private bool _isFilteringActive;
        private bool _scrapDone;

        public string ServiceName => "TrattoriSupermarket.it";
        public string Country => "IT";
        public bool IsAuction => false;
        public bool IsCompany => false;
        public bool RequiresText => true;

        public TrattoriSupermarketScrapingService(LoggingService loggingService)
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
                    _scrapBrowser.Navigate($"http://www.trattorisupermarket.it/en/search.html?text={query}&ps=60&pg=1");
                else
                    _scrapBrowser.Navigate($"http://www.trattorisupermarket.it/en/search.html?text={query}&ps=60&pg={page}");
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, "Unhandled exception in ScrapTrattoriSupermarketWebBrowserAsync");
            }
        }

        private void ScrapBrowserLoadCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            if (_scrapDone || _scrapBrowser.ReadyState != WebBrowserReadyState.Complete)
                return;
            IEnumerable<HtmlElement> ads = null;
            string[] filters = null;
            string[] searchTextWords = _searchText.ToLower().Split(' ');
            if (!string.IsNullOrEmpty(_synonyms))
                filters = _synonyms.ToLower().Split(';');
            var results = new List<Advertisement>();
            try
            {
                ads = _scrapBrowser.Document.ElementsByClass("div", "list-item-row");
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
                foreach (var add in ads)
                {
                    var row = add.ElementsByClass("div", "row")?.FirstOrDefault();
                    if (row == null)
                        continue;
                    var title = SafeExtractTitle(row);
                    bool continueFlag = false;
                    bool breakFlag = false;
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
                    var price = SafeExtractPrice(row);
                    if (!string.IsNullOrWhiteSpace(price))
                    {
                        if (price.ToLower().Contains("upon request"))
                            price = "POA";
                        else
                        {
                            price = price.Replace(",", "").Replace("€", "");
                            if (decimal.TryParse(price, NumberStyles.Any, NumberFormatInfo.InvariantInfo, out decimal decimalPrice))
                            {
                                price = decimalPrice.ToString("### ###") + " €";
                                if (decimalPrice <  _lastCostMin)
                                    continue;
                                if (decimalPrice > _lastCostMax)
                                    continue;
                            }
                        }

                    }
                    var descriptionDiv = add.ElementsByClass("table", "table")?.FirstOrDefault();
                    var description = descriptionDiv?.InnerText;
                    var photoDiv = add.ElementsByClass("div", "photo-list")?.FirstOrDefault();
                    var src = SafeExtractSrc(photoDiv);
                    if (src == null || src == " " || src == "")
                        src = "Images/noimage.png";
                    var href = SafeExtractHref(photoDiv);

                    results.Add(new Advertisement
                    {
                        Name = title,
                        Description = description,
                        Price = price,
                        ImageUrl = src,
                        PageUrl = href,
                    });
                }
                var hasMorePages = ScrapHasMorePages(_scrapBrowser.Document);
                _scrapDone = true;
                AsyncScrapCompleted?.Invoke(this, results, hasMorePages, null);
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, "Unhandled exception in ScrapBrowserLoadCompleted");
                AsyncScrapCompleted?.Invoke(this, null, false, "Unhandled exception");
            }
            finally
            {
                if (_scrapBrowser != null)
                {
                    _scrapBrowser.Dispose();
                    _scrapBrowser = null;
                }
            }
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
        private string SafeExtractTitle(HtmlElement simpleAdd)
        {
            var el = simpleAdd?.GetElementsByTagName("h2");
            if (el == null)
                return null;
            if (el.Count == 0)
                return null;
            return el[0]?.InnerText;
        }
        private string SafeExtractPrice(HtmlElement simpleAdd)
        {
            var el = simpleAdd?.GetElementsByTagName("h3");
            if (el == null)
                return null;
            if (el.Count == 0)
                return null;
            return el[0]?.InnerText;
        }
    }
}
