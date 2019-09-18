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
using System.Net;
using System.Globalization;
using AgroAdd.Services.Scrappers.MarktplaatsScrapper.Models;

namespace AgroAdd.Services.Scrappers.MarktplaatsScrapper
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

        public string ServiceName => "Maarktplaats.nl";
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
                    _scrapBrowser.Navigate($"https://www.marktplaats.nl/l/zakelijke-goederen/q/{query}/");
                else
                    _scrapBrowser.Navigate($"https://www.marktplaats.nl/l/zakelijke-goederen/q/{query}/p/{page}/");
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
                string[] filters = null;
                string[] searchTextWords = _searchText.ToLower().Split(' ');
                string apiResponse = _scrapBrowser.Document.Body.InnerHtml;
                var start = apiResponse.IndexOf("{");
                var end = apiResponse.LastIndexOf("}</SCRIPT>");
                apiResponse = apiResponse.Substring(start, end - start + 1).Trim();
                if (!string.IsNullOrEmpty(_synonyms))
                    filters = _synonyms.ToLower().Split(';');

                var results = new List<Advertisement>();
                var parsedResponse = JsonConvert.DeserializeObject<MarktplaatsSearchResponse>(apiResponse);

                if (parsedResponse == null || !parsedResponse.props.pageProps.listings.Any())
                {
                    AsyncScrapCompleted?.Invoke(this, results, false, null);
                }

                foreach (var add in parsedResponse.props.pageProps.listings)
                {
                    string img;
                    string price;
                    string title;
                    bool continueFlag = false;
                    bool breakFlag = false;
                    if (add.imageUrls.Any())
                        img = "https:" + add.imageUrls[0];
                    else
                        img = "Images/noimage.png";
                    title = add?.title;

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

                    if (add.priceInfo.priceType.ToUpper() != "FIXED")
                        price = "POA";
                    else
                    {
                        price = (add.priceInfo.priceCents / 100).ToString();
                        if (decimal.TryParse(price, NumberStyles.Any, NumberFormatInfo.InvariantInfo, out decimal decimalPrice))
                        {
                            price = decimalPrice.ToString("#,##0") + " €";
                            if (_lastCostMin.HasValue && decimalPrice < _lastCostMin)
                                continue;
                            if (_lastCostMax.HasValue && decimalPrice > _lastCostMax)
                                continue;
                        }
                    }

                    results.Add(new Advertisement
                    {
                        Name = title,
                        Description = add.description + Environment.NewLine +
                        add.sellerInformation.sellerName,
                        Price = price,
                        ImageUrl = img,
                        PageUrl = "https://www.marktplaats.nl" + add?.vipUrl,
                    });
                }
                bool hasNextPage;
                decimal resultCount = parsedResponse.props.pageProps.totalResultCount;
                decimal totalPages = Math.Ceiling(resultCount / 30);

                if (parsedResponse.props.pageProps.page + 1 < totalPages)
                    hasNextPage = true;
                else
                    hasNextPage = false;

                AsyncScrapCompleted?.Invoke(this, results, hasNextPage, null);
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
