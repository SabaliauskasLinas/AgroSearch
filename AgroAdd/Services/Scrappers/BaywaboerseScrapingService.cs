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
    public class BaywaboerseScrapingService : IScrapingService
    {
        private readonly LoggingService _loggingService;
        private WebBrowser _scrapBrowser;
        private int? _lastCostMin;
        private int? _lastCostMax;
        private string _synonyms;
        private string _searchText;
        private bool _isFilteringActive;
        private bool _scrapDone;

        public string ServiceName => "Baywaboerse.com";
        public string Country => "DE";
        public bool IsAuction => false;
        public bool IsCompany => false;
        public bool RequiresText => true;

        public BaywaboerseScrapingService(LoggingService loggingService)
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
            try
            {
                if (_scrapBrowser == null)
                {
                    _scrapBrowser = new WebBrowser();
                    _scrapBrowser.DocumentCompleted += ScrapBrowserLoadCompleted;
                    _scrapBrowser.ScriptErrorsSuppressed = true;
                }
                if (page < 2)
                    _scrapBrowser.Navigate($"https://www.baywaboerse.com/gebraucht?phrase={query}&category=&manufacturer=&country=&radius_zipcode=&radius_distance_km=&price_to=&power_from=&year_from=");
                else
                    _scrapBrowser.Navigate($"https://www.baywaboerse.com/gebraucht/list/{page}?phrase={query}&category=&manufacturer=&country=&radius_zipcode=&radius_distance_km=&price_to=&power_from=&year_from=");
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, "Unhandled exception in ScrapBaywaboerseWebBrowserAsync");
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
                ads = _scrapBrowser.Document.ElementsByClass("div", "list-item");
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
                    var info = add.ElementsByClass("div", "padded-box-horizontal")?.FirstOrDefault();
                    if (info == null)
                        continue;
                    var title = SafeExtrctTitle(info);
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
                    var price = add.ElementsByClass("div", "price")?.FirstOrDefault();
                    var realPrice = price?.ElementsByClass("span", "priceStr")?.FirstOrDefault()?.InnerText;
                    if (realPrice == null)
                        realPrice = "POA";
                    else
                    {
                        realPrice = realPrice.Replace(",", "").Replace("€", "").Replace(".","");
                        if (decimal.TryParse(realPrice, NumberStyles.Any, NumberFormatInfo.InvariantInfo, out decimal decimalPrice))
                        {
                            realPrice = decimalPrice.ToString("### ###") + " €";
                            if (_lastCostMin.HasValue && decimalPrice < _lastCostMin)
                                continue;
                            if (_lastCostMax.HasValue && decimalPrice > _lastCostMax)
                                continue;
                        }
                    }
                    var description = info.ElementsByClass("div", "desc").FirstOrDefault()?.InnerText + add.ElementsByClass("div", "location").FirstOrDefault()?.InnerText;
                    var divImg = add.ElementsByClass("div", "image")?.FirstOrDefault();
                    string src = SafeExtractSrc(divImg);
                    if (src == null || src == " " || src == "")
                        src = "Images/noimage.png";
                    else if (!src.Contains("http"))
                        src = "https://www.baywaboerse.com/" + src;
                    var href = SafeExtractHref(add);


                    results.Add(new Advertisement
                    {
                        Name = title,
                        Description = description,
                        Price = realPrice,
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
                var pagination = document.ElementsByClass("div", "pagination")?.FirstOrDefault();
                if (pagination.ElementsByClass("a", "next")?.FirstOrDefault()?.InnerText != null)
                    return true;
                else
                    return false;
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, "Failed to parse pagination");
                return false;
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
            return el[0].GetAttribute("data-original");
        }
        private string SafeExtrctTitle(HtmlElement simpleAdd)
        {
            var el = simpleAdd?.GetElementsByTagName("h2");
            if (el == null)
                return null;
            if (el.Count == 0)
                return null;
            return el[0].InnerText;
        }
    }
}
