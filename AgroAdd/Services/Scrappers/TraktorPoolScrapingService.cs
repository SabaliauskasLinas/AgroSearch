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
    public class TraktorPoolScrapingService : IScrapingService
    {
        private readonly LoggingService _loggingService;
        private WebBrowser _scrapBrowser;
        private int? _lastCostMin;
        private int? _lastCostMax;
        private bool _scrapDone;

        public string ServiceName => "TraktorPool.de";
        public string Country => "DE";
        public bool IsAuction => false;
        public bool IsCompany => false;
        public bool RequiresText => true;

        public TraktorPoolScrapingService(LoggingService loggingService)
        {
            _loggingService = loggingService;
        }

        public event ScrapCompleted AsyncScrapCompleted;

        public void ScrapAsync(string query, int? costmin, int? costmax, int page = 1)
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
                    _scrapBrowser.Navigate($"https://www.traktorpool.de/gebraucht/b-Traktoren/95/keywords/{query}/results/50/sortby/score/");
                else
                    _scrapBrowser.Navigate($"https://www.traktorpool.de/gebraucht/b-Traktoren/95/keywords/{query}/page/{page}/results/50/sortby/score/");
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, "Unhandled exception in ScrapTraktorPoolWebBrowserAsync");
            }
        }

        private void ScrapBrowserLoadCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            if (_scrapDone || _scrapBrowser.ReadyState != WebBrowserReadyState.Complete)
                return;
            IEnumerable<HtmlElement> ads = null;
            var results = new List<Advertisement>();
            try
            {
                ads = _scrapBrowser.Document.ElementsByClass("div", "machine_list_entry");
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
                    var price = add.ElementsByClass("span", "machine_entry_price_price")?.FirstOrDefault()?.InnerText;
                    if (price == null)
                    {
                        price = add.ElementsByClass("span", "machine_entry_price_price specialOrange")?.FirstOrDefault()?.InnerText;
                    }
                    if (!string.IsNullOrWhiteSpace(price))
                    {
                        if (price.ToLower().Contains("preis auf anfrage"))
                            price = "POA";
                        if (price.ToLower().Contains("auktion"))
                            price = "Auction";
                        else
                        {
                            price = price.Replace(".","").Replace(",", "").Replace("€", "");
                            if (decimal.TryParse(price, NumberStyles.Any, NumberFormatInfo.InvariantInfo, out decimal decimalPrice))
                            {
                                price = decimalPrice.ToString("#,##0") + " €";
                                if (decimalPrice <  _lastCostMin)
                                    continue;
                                if (decimalPrice > _lastCostMax)
                                    continue;
                            }
                        }

                    }
                    var title = add.ElementsByClass("span", "machine_entry_title")?.FirstOrDefault()?.InnerText;
                    var description = add.ElementsByClass("span", "machine_entry_description")?.FirstOrDefault()?.InnerText;
                    var div = add.ElementsByClass("div", "machine_entry_image")?.FirstOrDefault();
                    var src = SafeExtractSrc(div);
                    if (src != null && src[0] != 'h')
                        src = "https://www.traktorpool.de" + src;
                    else if(src == null || src == " " || src == "")
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
                var pagination = document.ElementsByClass("div", "paginatorNavigation")?.FirstOrDefault();
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
                if (rel.Contains("›"))
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
            if (el[0].GetAttribute("src") == "https://www.traktorpool.de/images/lazyloadtrans.gif")
                return el[0].GetAttribute("data-original");

            return el[0].GetAttribute("src");
        }

    }
}
