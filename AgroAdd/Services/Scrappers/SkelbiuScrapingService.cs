using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using AgroAdd.Models;
using AgroAdd.Extensions;
using AgroAdd.Interfaces;
using System.Globalization;

namespace AgroAdd.Services.Scrappers
{
    public class SkelbiuScrapingService : IScrapingService
    {
        private readonly LoggingService _loggingService;
        private WebBrowser _scrapBrowser;
        private int? _lastCostMin;
        private int? _lastCostMax;
        private string _synonyms;
        private string _searchText;
        private bool _isFilteringActive;
        private bool _scrapDone;

        public string ServiceName => "Skelbiu.lt";
        public string Country => "LT";
        public bool IsAuction => false;
        public bool IsCompany => false;
        public bool RequiresText => true;

        public SkelbiuScrapingService(LoggingService loggingService)
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
                if(page < 2)
                    _scrapBrowser.Navigate($"https://www.skelbiu.lt/skelbimai/?keywords={query}&cities=0&distance=0&mainCity=0&search=1&category_id=3996&type=0&user_type=0&ad_since_min=0&ad_since_max=0&detailsSearch=0&facets=1&facets=0");
                else
                    _scrapBrowser.Navigate($"https://www.skelbiu.lt/skelbimai/{page}?keywords={query}&cities=0&distance=0&mainCity=0&search=1&category_id=3996&type=0&user_type=0&ad_since_min=0&ad_since_max=0&detailsSearch=0&facets=0");
            }
            catch(Exception ex)
            {
                _loggingService.LogException(ex, "Unhandled exception in ScrapSkelbiuWebBrowserAsync");
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
                ads = _scrapBrowser.Document.ElementsByClass("li", "simpleAds");
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
                foreach(var add in ads)
                {
                    var tDiv = add.ElementsByClass("div", "itemReview")?.FirstOrDefault();
                    var title = SafeExtractTitle(tDiv);
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
                    var price = add.ElementsByClass("div", "adsPrice")?.FirstOrDefault()?.InnerText;
                    if (price == null || price == " ")
                        price = "POA";
                    else
                    {
                        price = price.Replace(" ", "").Replace(",", "").Replace("€", "");
                        if (decimal.TryParse(price, NumberStyles.Any, NumberFormatInfo.InvariantInfo, out decimal decimalPrice))
                        {
                            price = decimalPrice.ToString("### ###") + " €";
                            if (decimalPrice <  _lastCostMin)
                                continue;
                            if (decimalPrice >  _lastCostMax)
                                continue;
                        }
                    }
                    var description = add.ElementsByClass("div", "adsTexts")?.FirstOrDefault()?.InnerText;
                    var href = SafeExtractHref(add);
                    var a = add.ElementsByClass("a", "adsImage")?.FirstOrDefault();
                    var src = SafeExtractSrc(a);
                    if (src == null || src == " " || src == "")
                        src = "Images/noimage.png";
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
            catch(Exception ex)
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
                var pagination = document.GetElementById("pagination");
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
                var rel = paginationElement?.GetElementsByTagName("a")[i].GetAttribute("rel");
                if (rel == "next")
                {
                    return paginationElement?.GetElementsByTagName("a")[i].GetAttribute("rel") == "next";
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
            var el = simpleAdd?.GetElementsByTagName("a");
            if (el == null)
                return null;
            if (el.Count == 0)
                return null;
            return el[0]?.InnerText;
        }

    }
}
