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
    public class MaskinbladetScrapingService : IScrapingService
    {
        private readonly LoggingService _loggingService;
        private readonly CurrencyApi _currencyApi;
        private WebBrowser _scrapBrowser;
        private int? _lastCostMin;
        private int? _lastCostMax;
        private bool _scrapDone;
        private decimal _currentRate = 0.13401m;
        private bool _rateLoaded = false;

        public string ServiceName => "Maskinbladet.dk";
        public string Country => "DK";
        public bool IsAuction => false;
        public bool IsCompany => false;
        public bool RequiresText => true;

        public MaskinbladetScrapingService(LoggingService loggingService, CurrencyApi currencyApi)
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
            if (_scrapBrowser == null)
            {
                _scrapBrowser = new WebBrowser();
                _scrapBrowser.DocumentCompleted += ScrapBrowserLoadCompleted;
                _scrapBrowser.ScriptErrorsSuppressed = true;
            }
            try
            {
                if (page < 2)
                    _scrapBrowser.Navigate($"http://www.maskinbladet.dk/maskiner/landbrugsmaskiner-c1e8ae347-0cba-4690-aa94-e2684c779158?query={query}&rows=50");
                else
                    _scrapBrowser.Navigate($"http://www.maskinbladet.dk/maskiner/landbrugsmaskiner-c1e8ae347-0cba-4690-aa94-e2684c779158?query={query}&rows=50&p={page}");

                if (!_rateLoaded)
                {
                    _currencyApi.GetRateAsync(CurrencyTypes.DanishKrone)
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
                _loggingService.LogException(ex, "Unhandled exception in ScrapMaskinbladetWebBrowserAsync");
            }
        }

        private void ScrapBrowserLoadCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            if (_scrapDone || _scrapBrowser.ReadyState == WebBrowserReadyState.Loading || _scrapBrowser.ReadyState == WebBrowserReadyState.Loaded || _scrapBrowser.ReadyState == WebBrowserReadyState.Uninitialized)
                return;

            //while (!_rateLoaded)
            {
                //System.Threading.Thread.Sleep(500);
            }

            HtmlElementCollection ads = null;
            var results = new List<Advertisement>();
            try
            {
                var div = _scrapBrowser.Document.ElementsByClass("div", "machine-search").FirstOrDefault();
                if (div == null)
                {
                    AsyncScrapCompleted?.Invoke(this, results, false, null);
                    return;
                }
                ads = div.GetElementsByTagName("a");
                if (ads.Count == 0)
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
               
                foreach (HtmlElement add in ads)
                {
                    var titleDiv = add.ElementsByClass("div", "forhandler-beskrivelse")?.FirstOrDefault();
                    if (titleDiv == null)
                        continue;
                    var price = add.ElementsByClass("p", "forhandler-pris-align-shift")?.FirstOrDefault()?.InnerText;
                    if (!string.IsNullOrWhiteSpace(price))
                    {
                        if (price.ToLower().Contains("ring for pris"))
                            price = "POA";
                        else
                        {
                            price = price.Replace("Pris: ","").Replace("-","").Replace(".", "").Replace(",", "").Replace("€", "");
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
                    var href = add.GetAttribute("href");
                    var description = titleDiv.ElementsByClass("p", "description")?.FirstOrDefault()?.InnerText+ Environment.NewLine + titleDiv.ElementsByClass("p", "location-info")?.FirstOrDefault()?.InnerText;
                    var divAd = add.ElementsByClass("div", "hidden-xs");
                    var moreDescription = ElementAtOrDefault(divAd, 0)?.InnerText;
                    var moremoreDescription = ElementAtOrDefault(divAd, 1)?.InnerText;
                    var moremoremoreDescription = ElementAtOrDefault(divAd, 2)?.InnerText;
                    description = moreDescription + " " + moremoreDescription + " " + moremoremoreDescription + Environment.NewLine + description;
                    var title = titleDiv?.GetElementsByTagName("h2")[0].InnerText;
                    var srcDiv = add.ElementsByClass("div", "search-result-image")?.FirstOrDefault();
                    var imgSrc = SafeExtractSrc(srcDiv);
                    if (imgSrc == null || imgSrc == " " || imgSrc == "")
                        imgSrc = "Images/noimage.png";

                    results.Add(new Advertisement
                    {
                        Name = title,
                        Description = description,
                        Price = price,
                        ImageUrl = imgSrc,
                        PageUrl = href,
                    });
                }
                var hasMorePages = ScrapHasMorePages(_scrapBrowser.Document);
                _scrapDone = true;
                _scrapBrowser.Stop();
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
                if (rel.Contains("»"))
                {
                    return true;
                }
            }
            return false;
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
        private T ElementAtOrDefault<T>(IEnumerable<T> collection, int index)
        {
            if (collection == null || collection.Count() <= index)
                return default(T);
            return collection.ElementAt(index);
        }
    }
}
