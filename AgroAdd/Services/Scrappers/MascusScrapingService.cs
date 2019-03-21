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
    public class MascusScrapingService : IScrapingService
    {
        private readonly LoggingService _loggingService;
        private readonly CurrencyApi _currencyApi;
        private WebBrowser _scrapBrowser;
        private int? _lastCostMin;
        private int? _lastCostMax;
        private bool _scrapDone;
        private decimal _currentRate = 0.88245m;
        private bool _rateLoaded = false;

        public string ServiceName => "Mascus.co.uk";
        public string Country => "UK";
        public bool IsAuction => false;
        public bool IsCompany => false;
        public bool RequiresText => true;

        public MascusScrapingService(LoggingService loggingService, CurrencyApi currencyApi)
        {
            _loggingService = loggingService;
            _currencyApi = currencyApi;
        }

        public event ScrapCompleted AsyncScrapCompleted;

        public void ScrapAsync(string query, string filters, int? costmin, int? costmax, int page = 1)
        {
            _lastCostMin = costmin;
            _lastCostMax = costmax;
            _scrapDone = false;
            if (_scrapBrowser == null)
            {
                _scrapBrowser = new WebBrowser();
                _scrapBrowser.DocumentCompleted += ScrapBrowserLoadCompleted;
                _scrapBrowser.ScriptErrorsSuppressed = true;
            }
            try
            {
                if (page < 2)
                    _scrapBrowser.Navigate($"https://www.mascus.com/{query}/categorypath%3dagriculture/+/1,50,relevance,search.html");
                else
                    _scrapBrowser.Navigate($"https://www.mascus.com/{query}/categorypath%3dagriculture/+/{page},50,relevance,search.html");

                if (!_rateLoaded)
                {
                    _currencyApi.GetRateAsync(CurrencyTypes.UnitedStatesDollar)
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
                _loggingService.LogException(ex, "Unhandled exception in ScrapMascusWebBrowserAsync");
            }
        }

        private void ScrapBrowserLoadCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            if (_scrapDone || _scrapBrowser.ReadyState != WebBrowserReadyState.Complete)
                return;

            //while (!_rateLoaded)
            {
                //System.Threading.Thread.Sleep(500);
            }

            IEnumerable<HtmlElement> ads = null;
            var results = new List<Advertisement>();
            try
            {
                ads = _scrapBrowser.Document.ElementsByClass("li", "single-result");
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
                    var price = add.ElementsByClass("div", "result-price")?.FirstOrDefault()?.InnerText;
                    if (price == null)
                        continue;
                    if (!string.IsNullOrWhiteSpace(price))
                    {
                        if (price.ToUpper().Contains("ON REQUEST"))
                            price = "POA";
                        else
                        {
                            price = price.Replace(",", "").Replace("USD", "");
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
                    var title = add.ElementsByClass("a", "title-font")?.FirstOrDefault()?.InnerText;
                    var description = add.ElementsByClass("p", "result-details")?.FirstOrDefault()?.InnerText;
                    var td = add.ElementsByClass("td", "result-image")?.FirstOrDefault();
                    var src = SafeExtractSrc(td);
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
                var pageNumbers = document.ElementsByClass("div", "page-numbers-wrap")?.FirstOrDefault();
                var pagination = pageNumbers.ElementsByClass("ul", "page-numbers")?.FirstOrDefault();
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
            var li = paginationElement?.GetElementsByTagName("li");
            for (int i=0;i < li.Count;i++)
            {
                if(li[i].GetAttribute("className") == "current-page" && i != li.Count - 1)
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
    }
}
