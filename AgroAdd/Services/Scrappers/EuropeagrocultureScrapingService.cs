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
    public class EuropeagrocultureScrapingService : IScrapingService
    {
        private readonly LoggingService _loggingService;
        private WebBrowser _scrapBrowser;
        private int? _lastCostMin;
        private int? _lastCostMax;
        private string _synonyms;
        private string _searchText;
        private bool _isFilteringActive;
        private bool _scrapDone;

        public string ServiceName => "Europe-Agroculture.com";
        public string Country => "EU";
        public bool IsAuction => false;
        public bool IsCompany => false;
        public bool RequiresText => true;

        public EuropeagrocultureScrapingService(LoggingService loggingService)
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
                    _scrapBrowser.Navigate($"https://www.europe-agriculture.com/used-farming-equipments/f7/farming-equipments-ads.html?q={query}");
                else
                    _scrapBrowser.Navigate($"https://www.europe-agriculture.com/used-farming-equipments/f7/farming-equipments-ads.html?p={page}&q={query}");
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, "Unhandled exception in ScrapEuropeAgrocultureWebBrowserAsync");
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
                ads = _scrapBrowser.Document.ElementsByClass("div", "row-listing");
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
                    var divTit = add.ElementsByClass("div", "title")?.FirstOrDefault();
                    if (divTit == null)
                        continue;
                    var title = divTit?.InnerText;
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
                    var description = add?.ElementsByClass("div","listing-infos")?.FirstOrDefault()?.InnerText;
                    var price = add?.ElementsByClass("div", "listing-price")?.FirstOrDefault()?.InnerText;
                    if (price == null)
                        price = "POA";
                    else
                    {
                        price = price.Replace(",", "").Replace("€", "");
                        if (decimal.TryParse(price, NumberStyles.Any, NumberFormatInfo.InvariantInfo, out decimal decimalPrice))
                        {
                            price = decimalPrice.ToString("#,##0") + " €";
                            if (decimalPrice <  _lastCostMin)
                                continue;
                            if (decimalPrice >  _lastCostMax)
                                continue;
                        }
                    }
                    var divImg = add.ElementsByClass("div", "picture")?.FirstOrDefault();
                    var src = SafeExtratImgSrc(divImg);
                    if (src == null || src == " " || src == "")
                        src = "Images/noimage.png";
                    var href = SafeExtractHref(add);
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

        }
        private bool ScrapHasMorePages(HtmlDocument document)
        {
            try
            {
                var pagination = document?.ElementsByClass("ul", "pagination")?.FirstOrDefault();
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
            if (paginationElement?.ElementsByClass("i", "fa-chevron-right") != null)
                return true;
            else
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

        private string SafeExtratImgSrc(HtmlElement simpleElement)
        {
            var imgEl = simpleElement?.ElementsByClass("img", "img-responsive")?.FirstOrDefault();
            if (imgEl == null)
            {
                var imgElEl = imgEl?.GetAttribute("src");
                if (imgElEl == null)
                    return null;
                else if (imgElEl.Contains("lazy"))
                    return imgEl.GetAttribute("data-src");
                else
                    return imgElEl = imgEl?.GetAttribute("src");
            }
            return imgEl.GetAttribute("data-src");

        }
    }
}
