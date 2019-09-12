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
    public class MarktplaatsScrapingService : IScrapingService
    {
        private readonly LoggingService _loggingService;
        private WebBrowser _scrapBrowser;
        private int? _lastCostMin;
        private int? _lastCostMax;
        private string _synonyms;
        private string _searchText;
        private bool _isFilteringActive;
        private bool _scrapDone;
        private bool _rateLoaded;

        public string ServiceName => "Marktplaats.nl";
        public string Country => "NL";
        public bool IsAuction => false;
        public bool IsCompany => false;
        public bool RequiresText => true;

        public MarktplaatsScrapingService(LoggingService loggingService)
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
                    _scrapBrowser.Navigate($"https://www.marktplaats.nl/z/zakelijke-goederen/{query}.html?query={query}&categoryId=1085&view=gv");
                else
                    _scrapBrowser.Navigate($"https://www.marktplaats.nl/z/zakelijke-goederen/{query}.html?query={query}&categoryId=1085&currentPage={page}");
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, "Unhandled exception in ScrapMarktPlaatsWebBrowserAsync");
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
                ads = _scrapBrowser.Document.ElementsByClass("div", "mp-Listing-card-flex");
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
                    var descriptionDiv = add.ElementsByClass("div", "mp-Listing-card-flex-content--align-bottom")?.FirstOrDefault();
                    var price = descriptionDiv?.ElementsByClass("div", "mp-Listing-card-flex-price")?.FirstOrDefault()?.InnerText;
                    if (price == null)
                        continue;
                    var title = add.ElementsByClass("div", "mp-Listing-card-flex-title")?.FirstOrDefault()?.InnerText;
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
                    if (!string.IsNullOrWhiteSpace(price))
                    {
                        if (price.ToUpper().Contains("OP AANVRAAG"))
                            price = "On Request";
                        else if (price.ToUpper().Contains("BIEDEN"))
                            price = "Offer";
                        else if (price.ToUpper().Contains("N.O.T.K."))
                            price = "To be agreed";
                        else if (price.ToUpper().Contains("ZIE OMSCHRIJVING"))
                            price = "See Description";
                        else if (price.ToUpper().Contains("GERESERVEERD"))
                            price = "Reserved";
                        else
                        {
                            price = price.Replace(",", "").Replace(".", "").Replace("€", "");
                            if (decimal.TryParse(price, NumberStyles.Any, NumberFormatInfo.InvariantInfo, out decimal decimalPrice))
                            {
                                decimalPrice /= 100;
                                price = decimalPrice.ToString("#,##0") + " €";
                                if (decimalPrice <  _lastCostMin)
                                    continue;
                                if (decimalPrice >  _lastCostMax)
                                    continue;
                            }
                        }

                    }
                    var description = descriptionDiv.ElementsByClass("div", "mp-Listing-card-flex-location-block")?.FirstOrDefault()?.InnerText;
                    var srcDiv = add.ElementsByClass("div", "mp-Listing-card-flex-image-block")?.FirstOrDefault();
                    var src = SafeExtractSrc(srcDiv);
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
                var pagination = document?.ElementsByClass("div","pagination")?.FirstOrDefault();
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
            if (paginationElement.GetElementsByTagName("a") != null && paginationElement.GetElementsByTagName("a")[paginationElement.GetElementsByTagName("a").Count-1].GetAttribute("disabled") == "False")
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
