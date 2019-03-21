using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using AgroAdd.Models;
using AgroAdd.Extensions;
using AgroAdd.Interfaces;
using System.Globalization;

namespace AgroAdd.Services.Scrappers
{
    public class FarolHayGrassScrapingService : IScrapingService
    {
        private readonly LoggingService _loggingService;
        private readonly CurrencyApi _currencyApi;
        private WebBrowser _scrapBrowser;
        private int? _lastCostMin;
        private int? _lastCostMax;
        private bool _scrapDone;
        private decimal _currentRate = 1.15206m;
        private bool _rateLoaded;

        public string ServiceName => "         *Hay Grass";
        public string Country => "UK";
        public bool IsAuction => false;
        public bool IsCompany => true;
        public bool RequiresText => false;

        public FarolHayGrassScrapingService(LoggingService loggingService, CurrencyApi currencyApi)
        {
            _loggingService = loggingService;
            _currencyApi = currencyApi;
        }


        public event ScrapCompleted AsyncScrapCompleted;

        public void ScrapAsync(string query, string synonyms, bool filtering, int? costmin, int? costmax, int page = 1)
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
                    _scrapBrowser.Navigate($"https://www.farol.co.uk/product-list/hay-amp-grass/PVRLMO833551");
                if (!_rateLoaded)
                {
                    _currencyApi.GetRateAsync(CurrencyTypes.BritishPound)
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
                _loggingService.LogException(ex, "Unhandled exception in ScrapFarolWebBrowserAsync");
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

            var results = new List<Advertisement>();
            try
            {
                var table = _scrapBrowser.Document.ElementsByClass("table", "productList").FirstOrDefault();
                var ads = table.GetElementsByTagName("tr");
                if (ads.Count == 0)
                {
                    AsyncScrapCompleted?.Invoke(this, results, false, null);
                    return;
                }
                foreach (HtmlElement add in ads)
                {
                    if (add == ads[0])
                        continue;
                    var price = add.ElementsByClass("TD", "col_Price")?.FirstOrDefault()?.InnerText;
                    if (price == "Call Us")
                        price = "POA";
                    else
                    {
                        price = price.Replace(",", "").Replace("£", "").Replace(" ", "");
                        if (decimal.TryParse(price, NumberStyles.Any, NumberFormatInfo.InvariantInfo, out decimal decimalPrice))
                        {
                            var euroPrice = (int)(Math.Round(decimalPrice * _currentRate));
                            price = euroPrice.ToString("#,##0") + " €";
                            if (euroPrice < _lastCostMin)
                                continue;
                            if (euroPrice > _lastCostMax)
                                continue;
                        }
                    }
                    var description = "HP: " + add.ElementsByClass("TD", "col_HP")?.FirstOrDefault()?.InnerText + "\n"
                        + "Year: " + add.ElementsByClass("TD", "col_Year")?.FirstOrDefault()?.InnerText + "\n"
                        + "Hours: " + add.ElementsByClass("TD", "col_Hours")?.FirstOrDefault()?.InnerText + "\n"
                        + "Trans: " + add.ElementsByClass("TD", "col_Trans")?.FirstOrDefault()?.InnerText + "\n"
                        + "Speed: " + add.ElementsByClass("TD", "col_Speed")?.FirstOrDefault()?.InnerText;
                    var hreftd = add.ElementsByClass("TD", "viewMoreButton")?.FirstOrDefault();
                    var href = SafeExtractHref(hreftd);
                    if (href != null && href[0] != 'h')
                        href = "https://www.farol.co.uk" + href;
                    var a = add.ElementsByClass("TD", "col_Image")?.FirstOrDefault();
                    var src = SafeExtractSrc(a);
                    if (src != null && src[0] != 'h')
                        src = "https://www.farol.co.uk" + src;
                    var title = add.ElementsByClass("TD", "col_Make")?.FirstOrDefault()?.InnerText + " "
                        + add.ElementsByClass("TD", "col_Model")?.FirstOrDefault()?.InnerText;
                    results.Add(new Advertisement
                    {
                        Name = title,
                        Description = description,
                        Price = price,
                        ImageUrl = src,
                        PageUrl = href,
                    });
                }
                var hasMorePages = false;
                _scrapDone = true;
                AsyncScrapCompleted?.Invoke(this, results, hasMorePages, null);
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, "Unhandled exception in ScrapBrowserLoadCompleted");
                AsyncScrapCompleted?.Invoke(this, null, false, "Unhandled exception");
            }
        }

        private bool HasNextPageIndicator(HtmlElement paginationElement)
        {
            for (int i = 0; i < paginationElement?.GetElementsByTagName("a").Count; i++)
            {
                var rel = paginationElement?.GetElementsByTagName("a")[i].GetAttribute("rel");
                if (rel == "next")
                {
                    return paginationElement?.GetElementsByTagName("a")[i].GetAttribute("rel") == "next";
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
