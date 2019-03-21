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
    public class TrucksScrapingService : IScrapingService
    {
        private readonly LoggingService _loggingService;
        private WebBrowser _scrapBrowser;
        private int? _lastCostMin;
        private int? _lastCostMax;
        private bool _scrapDone;

        public string ServiceName => "Trucks.nl";
        public string Country => "NL";
        public bool IsAuction => false;
        public bool IsCompany => false;
        public bool RequiresText => true;

        public TrucksScrapingService(LoggingService loggingService)
        {
            _loggingService = loggingService;
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
                    _scrapBrowser.Navigate($"https://www.trucks.nl/zoeken/?category=farm&freetext={query}");
                else
                    _scrapBrowser.Navigate($"https://www.trucks.nl/zoeken/?page={page}&category=farm&freetext={query}");
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, "Unhandled exception in ScrapTrucksWebBrowserAsync");
            }
        }

        private void ScrapBrowserLoadCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            if (_scrapDone || _scrapBrowser.ReadyState != WebBrowserReadyState.Complete)
                return;
            var results = new List<Advertisement>();
            IEnumerable<HtmlElement> cardsList = null;
            try
            {
                var vechiles = _scrapBrowser.Document.GetElementById("mainVehicles");
                if (vechiles != null)
                {
                    cardsList = vechiles.ElementsByClass("div","card");
                    if (cardsList == null && !cardsList.Any())
                    {
                        AsyncScrapCompleted?.Invoke(this, results, false, null);
                        return;
                    }
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

                foreach (var cardelement in cardsList)
                {
                    var detailedinformation = cardelement.ElementsByClass("div", "detailedinformation")?.FirstOrDefault();
                    var price = detailedinformation.ElementsByClass("div", "price")?.FirstOrDefault()?.InnerText;
                    if (!string.IsNullOrWhiteSpace(price))
                    {
                        if (price.ToLower().Contains("op aanvraag"))
                            price = "On Request";
                        else if (price.ToLower().Contains("bieden"))
                            price = "Offer";
                        else if (price.ToLower().Contains("vaste prijs"))
                            price = "Fixed Price";
                        else
                        {
                            price = price.Replace("Prijs: ", "").Replace(".", "").Replace(",", "").Replace("€", "").Replace("-", "").Replace(" ", "");
                            if (decimal.TryParse(price, NumberStyles.Any, NumberFormatInfo.InvariantInfo, out decimal decimalPrice))
                            {
                                price = decimalPrice.ToString("#,##0") + " €";
                                if (decimalPrice < _lastCostMin)
                                    continue;
                                if (decimalPrice > _lastCostMax)
                                    continue;
                            }
                        }

                    }
                    else
                        price = "PAO";
                    var description = detailedinformation.ElementsByClass("div", "year")?.FirstOrDefault()?.InnerText + "\n" + 
                        detailedinformation.ElementsByClass("div", "custom")?.FirstOrDefault()?.InnerText + "\n" + 
                        detailedinformation.ElementsByClass("div", "dealername")?.FirstOrDefault()?.InnerText;
                    var info = cardelement.ElementsByClass("div", "info")?.FirstOrDefault();
                    var title = info.ElementsByClass("span", "brand")?.FirstOrDefault()?.InnerText + " " +
                        info.ElementsByClass("span", "type")?.FirstOrDefault()?.InnerText;
                    var divSrc = cardelement.ElementsByClass("div", "image")?.FirstOrDefault().ElementsByClass("div","single")?.FirstOrDefault();
                    var src = SafeExtractSrc(divSrc);
                    if (src != null && src[0] != 'h')
                        src = "https://www.trucks.nl" + src;
                    else if (src == null || src == " " || src == "")
                        src = "Images/noimage.png";
                    var href = SafeExtractHref(cardelement);

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
        }

        private bool ScrapHasMorePages(HtmlDocument document)
        {
            try
            {
                var pagination = document.ElementsByClass("ul", "paginationcontainer")?.FirstOrDefault();
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
            var arrow = paginationElement.ElementsByClass("li", "arrow")?.FirstOrDefault();
            if (arrow?.InnerText == ">")
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
            var el = simpleAdd?.GetElementsByTagName("source");
            if (el == null)
                return null;
            if (el.Count == 0)
                return null;
            return el[0].GetAttribute("srcset");
        }

    }
}
