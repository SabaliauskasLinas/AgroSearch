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
using System.ComponentModel;

namespace AgroAdd.Services.Scrappers
{
    public class TrucksScrapingService : IScrapingService
    {
        private readonly LoggingService _loggingService;
        private WebBrowser _scrapBrowser;
        private int? _lastCostMin;
        private int? _lastCostMax;
        private string _synonyms;
        private string _searchText;
        private bool _isFilteringActive;
        private bool _scrapDone;

        public string ServiceName => "Trucks.nl";
        public string Country => "NL";
        public bool IsAuction => false;
        public bool IsCompany => false;
        public bool RequiresText => true;

        public TrucksScrapingService(LoggingService loggingService)
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
                    _scrapBrowser.Navigate($"https://www.trucksnl.com/agricultural-machinery?q={query}");
                else
                    _scrapBrowser.Navigate($"https://www.trucksnl.com/agricultural-machinery?q={query}&page={page}");
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, "Unhandled exception in ScrapTrucksWebBrowserAsync");
            }
        }

        private void ScrapBrowserLoadCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            if (_scrapDone || _scrapBrowser.ReadyState != WebBrowserReadyState.Complete)
                return;
            var results = new List<Advertisement>();
            string[] filters = null;
            string[] searchTextWords = _searchText.ToLower().Split(' ');
            if (!string.IsNullOrEmpty(_synonyms))
                filters = _synonyms.ToLower().Split(';');
            IEnumerable<HtmlElement> cardsList = null;
            try
            {

                cardsList = _scrapBrowser.Document.ElementsByClass("div", "advertisement-list-item");
                if (cardsList == null && !cardsList.Any())
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

                foreach (var cardelement in cardsList)
                {
                    var title = cardelement.ElementsByClass("a", "card-title").FirstOrDefault().InnerText;
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
                    var price = cardelement.ElementsByClass("p", "card-text")?.FirstOrDefault()?.InnerText;
                    int trashIndex = price.LastIndexOf('\n');
                    price = price.Substring(trashIndex+1);
                    if (!string.IsNullOrWhiteSpace(price))
                    {
                        if (!Regex.IsMatch(price, @"^€[0-9]"))
                            price = "PAO";
                        else
                        {
                            price = price.Replace("Prijs: ", "").Replace(".", "").Replace(",", "").Replace("€", "").Replace("-", "").Replace(" ", "");
                            if (decimal.TryParse(price, NumberStyles.Any, NumberFormatInfo.InvariantInfo, out decimal decimalPrice))
                            {
                                price = decimalPrice.ToString("### ###") + " €";
                                if (decimalPrice < _lastCostMin)
                                    continue;
                                if (decimalPrice > _lastCostMax)
                                    continue;
                            }
                        }
                    }
                    else
                        price = "PAO";
                    var description = cardelement.ElementsByClass("ul", "list-properties")?.FirstOrDefault()?.InnerText;
                    var src = SafeExtractSrc(cardelement);
                    if (src == null || src == " " || src == "")
                        src = "Images/noimage.png";
                    var href = SafeExtractHref(cardelement);

                    results.Add(new Advertisement
                    {
                        Name = title,
                        Description = description,
                        Price = price,
                        ImageUrl = src,
                        PageUrl = href,
                    });
                }
                var pagination = _scrapBrowser.Document.ElementsByClass("ul", "pagination").FirstOrDefault();
                var hasMorePages = ScrapHasMorePages(pagination);
                _scrapDone = true;
                AsyncScrapCompleted?.Invoke(this, results, hasMorePages, null);
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, "Unhandled exception in ScrapBrowserLoadCompleted");
                AsyncScrapCompleted?.Invoke(this, null, false, "Unhandled exception");
            }
        }

        private bool ScrapHasMorePages(HtmlElement htmlElement)
        {
            var pageItemCount = htmlElement.ElementsByClass("li", "page-item").ToList().Count;
            var pageItems = htmlElement.ElementsByClass("li", "page-item").ToList();
            while (pageItemCount != 0)
            {
                if (pageItems[pageItemCount - 1].InnerText.ToUpper().Contains("NEXT"))
                    return true;
                pageItemCount--;
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
            if (el[0].GetAttribute("src") == null || el[0].GetAttribute("src") == "")
                return el[0].GetAttribute("data-src");
            return el[0].GetAttribute("src");
        }

    }
}
