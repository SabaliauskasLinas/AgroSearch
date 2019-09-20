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
    public class ClassBordesholmScrapingService : IScrapingService
    {
        private readonly LoggingService _loggingService;
        private WebBrowser _scrapBrowser;
        private int? _lastCostMin;
        private int? _lastCostMax;
        private bool _scrapDone;

        public string ServiceName => "Class-Bordesholm.de";
        public string Country => "DE";
        public bool IsAuction => false;
        public bool IsCompany => false;
        public bool RequiresText => false;

        public ClassBordesholmScrapingService(LoggingService loggingService)
        {
            _loggingService = loggingService;
        }

        public event ScrapCompleted AsyncScrapCompleted;


        public void ScrapAsync(string query, string synonyms, bool filtering, int? costmin, int? costmax, int page = 1)
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

                _scrapBrowser.Navigate($"https://www.claas-bordesholm.de/de/gebrauchtmaschinen?&rewrite=--fname=getMasch3--hauptgrup%20peid=0--rownum=300--idWahrung=1--plz=0--land=undefined");

            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, "Unhandled exception in ScrapClassBordesholmAsync");
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
                ads = _scrapBrowser.Document.ElementsByClass("div", "maschList");
                
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
                    var price = add.ElementsByClass("div", "price")?.FirstOrDefault();
                    var realPrice = price?.ElementsByClass("span", "js-priceToChange")?.FirstOrDefault()?.InnerText;
                    if (realPrice == null)
                        realPrice = "POA";
                    else
                    {
                        realPrice = realPrice.Replace(",", "").Replace("€", "");
                        if (decimal.TryParse(realPrice, NumberStyles.Any, NumberFormatInfo.InvariantInfo, out decimal decimalPrice))
                        {
                            realPrice = decimalPrice.ToString("### ###") + " €";
                            if (_lastCostMin.HasValue && decimalPrice < _lastCostMin)
                                continue;
                            if (_lastCostMax.HasValue && decimalPrice > _lastCostMax)
                                continue;
                        }
                    }
                    var title = add.ElementsByClass("a", "link").FirstOrDefault()?.InnerText;
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
                        ImageUrl = "http:" + src,
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
