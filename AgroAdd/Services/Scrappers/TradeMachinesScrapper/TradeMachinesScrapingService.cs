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

namespace AgroAdd.Services.Scrappers.TradeMachinesScrapper
{
    public class TradeMachinesScrapingService : IScrapingService
    {
        private readonly LoggingService _loggingService;
        private WebBrowser _scrapBrowser;
        private int? _lastCostMin;
        private int? _lastCostMax;
        private string _synonyms;
        private string _searchText;
        private bool _isFilteringActive;

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
                if (apiResponse.IndexOf("json\">{") != -1)
                    apiResponse = apiResponse.Substring(apiResponse.IndexOf("json\">{"));
                var start = apiResponse.IndexOf("{");
                var end = apiResponse.LastIndexOf("}");
                apiResponse = apiResponse.Substring(start, end-start+1).Trim();
                if (!string.IsNullOrEmpty(_synonyms))
                    filters = _synonyms.ToLower().Split(';');

                var results = new List<Advertisement>();
                var parsedResponse = JsonConvert.DeserializeObject<TradeMachinesSearchResponse>(apiResponse);

                if (parsedResponse == null || !parsedResponse.props.pageProps.resultsState._originalResponse.results[0].hits.Any())
                {
                    AsyncScrapCompleted?.Invoke(this, results, false, null);
                }

                foreach (var add in parsedResponse.props.pageProps.resultsState._originalResponse.results[0].hits)
                {
                    string img;
                    string price;
                    string title;
                    bool continueFlag = false;
                    bool breakFlag = false;
                    if (add.product.isSparePart == true) continue;
                    if (add.hasImg)
                        img = "https://dizv3061bgivy.cloudfront.net" + add.imgId;
                    else
                        img = "Images/noimage.png";

                    if(add.fr.title != null && add.product.name != null)
                    {
                        if (add.fr.title.Length >= add.product.name.Length)
                            title = add.fr.title;
                        else
                            title = add.product.name;
                    } 
                    else
                    {
                        if (add.fr.title == null)
                            title = add.product.name;
                        else
                            title = add.fr.title;
                    }

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

                    if (!add.hasPrice)
                        price = "POA";
                    else
                    {
                        price = add.price.ToString();
                        if (decimal.TryParse(price, NumberStyles.Any, NumberFormatInfo.InvariantInfo, out decimal decimalPrice))
                        {
                            price = decimalPrice.ToString("### ###") + " €";
                            if (_lastCostMin.HasValue && decimalPrice < _lastCostMin)
                                continue;
                            if (_lastCostMax.HasValue && decimalPrice > _lastCostMax)
                                continue;
                        }
                    }

                    results.Add(new Advertisement
                    {
                        Name = title,
                        Description = "Year: " + add?.year +
                        Environment.NewLine + add.location?.country + " " + add.location?.state + " " + add.location?.city +
                        Environment.NewLine + add.seller?.name,
                        Price = price,
                        ImageUrl = img,
                        PageUrl = "https://trademachines.fr/lots/" + add?.objectID,
                    });
                }
                var parsedResponseContent = parsedResponse.props.pageProps.resultsState.content;
                bool hasNextPage;
                if (parsedResponseContent.nbPages > parsedResponseContent.page + 1)
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
