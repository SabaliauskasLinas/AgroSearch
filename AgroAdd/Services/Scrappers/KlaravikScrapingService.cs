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
    public class KlaravikScrapingService : IScrapingService
    {
        private readonly LoggingService _loggingService;
        private WebBrowser _scrapBrowser;
        private bool _scrapDone;
        private decimal _currentRate = 0.095m;

        public string ServiceName => "Klaravik.se";
        public string Country => "SE";
        public bool IsAuction => true;
        public bool IsCompany => false;
        public bool RequiresText => true;

        public KlaravikScrapingService(LoggingService loggingService)
        {
            _loggingService = loggingService;
        }

        public event ScrapCompleted AsyncScrapCompleted;

        public void ScrapAsync(string query, string filters, int? costmin, int? costmax, int page = 1)
        {
            _scrapDone = false;
            if (_scrapBrowser == null)
            {
                _scrapBrowser = _scrapBrowser ?? new WebBrowser();
                _scrapBrowser.DocumentCompleted += ScrapBrowserLoadCompleted;
                _scrapBrowser.ScriptErrorsSuppressed = true;
            }
            try
            {
                if (page < 2)
                    _scrapBrowser.Navigate($"https://www.klaravik.se/auction/1/?searchtext={query}");
                else
                    _scrapBrowser.Navigate($"https://www.klaravik.se/auction/{page}/?searchtext={query}");
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, "Unhandled exception in ScrapKlaravikWebBrowserAsync");
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
                ads = _scrapBrowser.Document.ElementsByClass("div", "listingBox").ToList();
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
                    var title = add.ElementsByClass("span", "objectMaker")?.FirstOrDefault()?.InnerText + add.ElementsByClass("span", "objectTitle")?.FirstOrDefault()?.InnerText;
                    if (title == "" || title == null)
                        continue;
                    var description = add.ElementsByClass("span", "end")?.FirstOrDefault()?.InnerText;
                    var price = add.ElementsByClass("div", "highestBid")?.FirstOrDefault()?.InnerText;
                    if (price == null)
                        price = "POA";
                    else
                    {
                        price = price.Replace(" ", "").Replace(",", "").Replace("kr", "");
                        if (decimal.TryParse(price, NumberStyles.Any, NumberFormatInfo.InvariantInfo, out decimal decimalPrice))
                        {
                            var euroPrice = (int)(Math.Round(decimalPrice * _currentRate));
                            price = euroPrice.ToString("#,##0") + " €";
                        }
                    }
                    var styles = SafeExtractStyles(add);
                    styles = styles?.Remove(0, styles.IndexOf("(") + 1);
                    var src = styles?.Remove(styles.IndexOf(")"));
                    if (src == null || src == " " || src == "")
                        src = "Images/noimage.png";
                    var href = SafeExtractHref(add);

                    results.Add(new Advertisement
                    {
                        Name = title,
                        Description = description,
                        Price = price,
                        ImageUrl = "https:"+src,
                        PageUrl = href,
                    });
                }
                _scrapDone = true;
                AsyncScrapCompleted?.Invoke(this, results, false, null);
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
                var pagination = document.ElementsByClass("ul", "paginator")?.FirstOrDefault();
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
                if (rel.Contains(">"))
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
        private string SafeExtractStyles(HtmlElement simpleAdd)
        {
            var el = simpleAdd?.GetElementsByTagName("div");
            if (el == null)
                return null;
            if (el.Count == 0)
                return null;
            var elel = el[0].Style?.ToLower()?.Split(';');
            if (!elel.Any())
                return null;
            return elel?.FirstOrDefault(x => x.StartsWith("background: url("));

        }
    }
}
