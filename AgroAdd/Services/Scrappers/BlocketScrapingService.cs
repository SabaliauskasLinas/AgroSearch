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
    public class BlocketScrapingService : IScrapingService
    {
        private readonly LoggingService _loggingService;
        private readonly CurrencyApi _currencyApi;
        private WebBrowser _scrapBrowser;
        private int? _lastCostMin;
        private int? _lastCostMax;
        private bool _scrapDone;
        private decimal _currentRate = 0.09442m;
        private bool _rateLoaded = false;

        public string ServiceName => "Blocket.se";
        public string Country => "SE";
        public bool IsAuction => false;
        public bool IsCompany => false;
        public bool RequiresText => true;


        public BlocketScrapingService(LoggingService loggingService, CurrencyApi currencyApi)
        {
            _loggingService = loggingService;
            _currencyApi = currencyApi;
        }

        public event ScrapCompleted AsyncScrapCompleted;

        public void ScrapAsync(string query, int? costmin, int? costmax, int page = 1)
        {
            _lastCostMin = costmin;
            _lastCostMax = costmax;
            _scrapDone = false;
            try
            {
                if (_scrapBrowser == null)
                {
                    _scrapBrowser = new WebBrowser();
                    _scrapBrowser.DocumentCompleted += ScrapBrowserLoadCompleted;
                    _scrapBrowser.ScriptErrorsSuppressed = true;
                }
                if (page < 2)
                    _scrapBrowser.Navigate($"https://www.blocket.se/stockholm/skogs-_lantbruksmaskiner?q={query}&w=3&st=s&ca=11&is=1&l=0&md=th&cg=1240&st=s");
                else
                    _scrapBrowser.Navigate($"https://www.blocket.se/hela_sverige?q={query}&cg=1240&w=3&st=s&ps=&pe=&c=&ca=11&l=0&md=th&o={page}");

                if (!_rateLoaded)
                {
                    _currencyApi.GetRateAsync(CurrencyTypes.SwedishKrone)
                        .ContinueWith((rateTask) =>
                        {
                            _rateLoaded = true;
                            if (rateTask.IsFaulted || rateTask.IsCanceled )
                                return;
                            if (rateTask.Result > 0)
                                 _currentRate = rateTask.Result;
                        });
                }
                
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, "Unhandled exception in ScrapBlocketWebBrowserAsync");
            }
        }

        private void ScrapBrowserLoadCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {

            if (_scrapDone || _scrapBrowser.ReadyState != WebBrowserReadyState.Complete)
                return;

            ////while (!_rateLoaded)
            {
                ////System.Threading.Thread.Sleep(500);
            }
            IEnumerable<HtmlElement> ads = null;
            var results = new List<Advertisement>();
            try
            {
                ads = _scrapBrowser.Document.ElementsByClass("article", "item_row");
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
                    var price = add.ElementsByClass("p", "list_price")?.FirstOrDefault()?.InnerText;
                    if (price != null && price.ToLower().Contains("ex. moms"))
                    {
                        price = price.Remove(price.IndexOf("("));
                    }

                    if (price == null)
                        price = "POA";
                    else
                    {
                        price = price.Replace(" ", "").Replace(",", "").Replace("kr", "");
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
                    var h1 = add.ElementsByClass("h1", "h5")?.FirstOrDefault();
                    var title = SafeExtractTitle(h1);
                    var divDes = add.ElementsByClass("header", "clearfix")?.FirstOrDefault();
                    var description = divDes.ElementsByClass("div", "pull-left")?.FirstOrDefault()?.InnerText;
                    var src = SafeExtractSrc(add);
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
                if (rel.Contains("Nästa sida"))
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
            var el = simpleAdd?.GetElementsByTagName("a");
            if (el == null)
                return null;
            if (el.Count == 0)
                return null;
            return el[0].GetAttribute("title");
        }

    }
}
