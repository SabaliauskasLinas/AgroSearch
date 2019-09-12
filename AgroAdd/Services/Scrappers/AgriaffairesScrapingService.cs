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
    public class AgriaffairesScrapingService : IScrapingService
    {
        private readonly LoggingService _loggingService;
        private WebBrowser _scrapBrowser;
        private int? _lastCostMin;
        private int? _lastCostMax;
        private string _synonyms;
        private string _searchText;
        private bool _isFilteringActive;
        private bool _scrapDone;

        public string ServiceName => "Agriaffaires.com";
        public string Country => "UK";
        public bool IsAuction => false;
        public bool IsCompany => false;
        public bool RequiresText => true;

        public AgriaffairesScrapingService(LoggingService loggingService)
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
                    _scrapBrowser.Navigate($"https://www.agriaffaires.co.uk/results/1/{query}.html");
                else
                    _scrapBrowser.Navigate($"https://www.agriaffaires.co.uk/results/{page}/{query}.html");
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, "Unhandled exception in ScrapAgriaffairesWebBrowserAsync");
            }
        }

        private void ScrapBrowserLoadCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            if (_scrapDone || _scrapBrowser.ReadyState != WebBrowserReadyState.Complete)
                return;
            IEnumerable<HtmlElement> ads = null;
            string[] filters = null;
            string[] searchTextWords = _searchText.ToLower().Split(' ');
            if(!string.IsNullOrEmpty(_synonyms))
                filters = _synonyms.ToLower().Split(';');
            var results = new List<Advertisement>();
            try
            {
                ads = _scrapBrowser.Document.ElementsByClass("div", "listing--element");
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
                    bool continueFlag = false;
                    bool breakFlag = false;
                    var title = add.ElementsByClass("a", "link").FirstOrDefault()?.InnerText;
                    //Checking if title contains each request text word
                    if (_isFilteringActive && !title.ToLower().Contains(_searchText.ToLower()))  
                    {
                        string testTitle = title.ToLower().Replace(" ", "");
                        foreach (var word in searchTextWords)
                        {
                            if(!testTitle.Contains(word))
                            {   
                                // Checking if title contains filters
                                if(filters != null && filters.Length != 0)
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
                    var realPrice = price?.ElementsByClass("span", "js-priceToChange")?.FirstOrDefault()?.InnerText;
                    if (realPrice == null)
                        realPrice = "POA";
                    else
                    {
                        realPrice = realPrice.Replace(",", "").Replace("€", "");
                        if (decimal.TryParse(realPrice, NumberStyles.Any, NumberFormatInfo.InvariantInfo, out decimal decimalPrice))
                        {
                            realPrice = decimalPrice.ToString("#,##0") + " €";
                            if (_lastCostMin.HasValue && decimalPrice <  _lastCostMin)
                                continue;
                            if (_lastCostMax.HasValue && decimalPrice >  _lastCostMax)
                                continue;
                        }
                    }
                    var description = add.ElementsByClass("div", "u-small").FirstOrDefault()?.InnerText + add.ElementsByClass("div", "listing--galerie--none").FirstOrDefault()?.InnerText;
                    var div = add.ElementsByClass("div", "img")?.FirstOrDefault();
                    var divSrc = div.ElementsByClass("div", "txtcenter")?.FirstOrDefault();
                    var src = SafeExtractSrc(divSrc);
                    if (src == null || src == " " || src == "")
                        src = "Images/noimage.png";
                    var href = SafeExtractHref(add);
                    

                    results.Add(new Advertisement
                    {
                        Name = title,
                        Description = description,
                        Price = realPrice,
                        ImageUrl ="http:" + src,
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
                if (pagination.ElementsByClass("li", "nav-right")?.FirstOrDefault()?.GetElementsByTagName("a")[0]?.GetAttribute("href") != null)
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
            return el[0].GetAttribute("data-src");
        }
    }
}
