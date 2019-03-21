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
    public class EuropeagrocultureScrapingService : IScrapingService
    {
        private readonly LoggingService _loggingService;
        private WebBrowser _scrapBrowser;
        private bool _scrapDone;
        private int? _lastCostMin;
        private int? _lastCostMax;

        public string ServiceName => "Europe-Agroculture.com";
        public string Country => "EU";
        public bool IsAuction => false;
        public bool IsCompany => false;
        public bool RequiresText => true;

        public EuropeagrocultureScrapingService(LoggingService loggingService)
        {
            _loggingService = loggingService;
        }

        public event ScrapCompleted AsyncScrapCompleted;

        public void ScrapAsync(string query, string synonyms, bool filtering, int? costmin, int? costmax, int page = 1)
        {
            _scrapDone = false;
            _lastCostMin = costmin;
            _lastCostMax = costmax;
            if (_scrapBrowser == null)
            {
                _scrapBrowser = new WebBrowser();
                _scrapBrowser.DocumentCompleted += ScrapBrowserLoadCompleted;
                _scrapBrowser.ScriptErrorsSuppressed = true;
            }
            try
            {
                if (page < 2)
                    _scrapBrowser.Navigate($"https://www.europe-agriculture.com/used-farming-equipments/f7/farming-equipments-ads.html?q={query}");
                else
                    _scrapBrowser.Navigate($"https://www.europe-agriculture.com/used-farming-equipments/f7/farming-equipments-ads.html?p={page}&q={query}");
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, "Unhandled exception in ScrapEuropeAgrocultureWebBrowserAsync");
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
                ads = _scrapBrowser.Document.ElementsByClass("div", "row-listing");
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
                    var divTit = add.ElementsByClass("div", "title")?.FirstOrDefault();
                    if (divTit == null)
                        continue;
                    var title = divTit.ElementsByClass("h3", "bold")?.FirstOrDefault()?.InnerText;
                    var divDivDes = add.ElementsByClass("div", "row")?.FirstOrDefault();
                    var divDes = divDivDes.ElementsByClass("div", "description")?.FirstOrDefault();
                    var description =divDes?.InnerText;
                    description = description?.Remove(description.IndexOf("Save"));
                    var identifier = divDes?.ElementsByClass("span", "hidden-gallery")?.FirstOrDefault()?.InnerText;
                    var price = "";
                    if (identifier.ToString().ToLower().Contains("auction sale"))
                    {
                        price = identifier.ToString();
                    }
                    else
                    {
                        price = divDes.ElementsByClass("span", "text-bold")?.FirstOrDefault()?.InnerText;

                            if (price == null)
                                price = "POA";
                            else
                            {
                                price = price.Replace(",", "").Replace("€", "");
                                if (decimal.TryParse(price, NumberStyles.Any, NumberFormatInfo.InvariantInfo, out decimal decimalPrice))
                                {
                                    price = decimalPrice.ToString("#,##0") + " €";
                                    if (decimalPrice <  _lastCostMin)
                                        continue;
                                if (decimalPrice >  _lastCostMax)
                                    continue;
                                }
                            }

                    }
                    var divImg = add.ElementsByClass("div", "picture")?.FirstOrDefault();
                    var src = SafeExtratImgSrc(divImg);
                    if (src == null || src == " " || src == "")
                        src = "Images/noimage.png";
                    var href = SafeExtractHref(divTit);
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
                var pagination = document?.ElementsByClass("ul", "pagination")?.FirstOrDefault();
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
            if (paginationElement?.ElementsByClass("i", "fa-chevron-right") != null)
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

        private string SafeExtratImgSrc(HtmlElement simpleElement)
        {
            var imgEl = simpleElement?.ElementsByClass("img", "corner")?.FirstOrDefault();
            if (imgEl == null)
            {
                var imgElEl = imgEl?.GetAttribute("src");
                if (imgElEl == null)
                    return null;
                else
                    return imgElEl = imgEl?.GetAttribute("src");
            }
            return imgEl.GetAttribute("data-src");

        }
    }
}
