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
    public class PfeiferMachineryScrapingService : IScrapingService
    {
        private readonly LoggingService _loggingService;
        private WebBrowser _scrapBrowser;
        private int? _lastCostMin;
        private int? _lastCostMax;
        private bool _scrapDone;

        public string ServiceName => "*PfeiferMachinery.com";
        public string Country => "NL";
        public bool IsAuction => false;
        public bool IsCompany => true;
        public bool RequiresText => false;

        public PfeiferMachineryScrapingService(LoggingService loggingService)
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
                    _scrapBrowser.Navigate($"https://www.pfeifermachinery.com/en/telehandlers?order_by=date&order_dir=DESC&manufacturer=&loadingcapacity=&boomlenghtmaximum=&fuel=&workareaheight=&liftheight=&state=&perpage=90");
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, "Unhandled exception in ScrapPfeiferMachineryWebBrowserAsync");
            }

        }

        private void ScrapBrowserLoadCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            if (_scrapDone || _scrapBrowser.ReadyState != WebBrowserReadyState.Complete)
                return;
            var results = new List<Advertisement>();
            IEnumerable<HtmlElement> ads = null;
            try
            {
                ads = _scrapBrowser.Document.ElementsByClass("div", "row").ToList()[8].ElementsByClass("div", "col-md-6");
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
                    var label = add.ElementsByClass("div", "img-wrapper")?.FirstOrDefault()?.InnerText;

                    if (label != null)
                        if (label.Contains("Sold"))
                            continue;
                    var wrapper = add.ElementsByClass("div", "wrapper")?.FirstOrDefault();
                    var price = wrapper.ElementsByClass("div", "price")?.FirstOrDefault()?.InnerText;
                    if (price != "PAO")
                    {
                        price = price.Replace(" ", "").Replace(",", "").Replace("€", "").Replace(".","").Replace("-","");
                        if (decimal.TryParse(price, NumberStyles.Any, NumberFormatInfo.InvariantInfo, out decimal decimalPrice))
                        {
                            price = decimalPrice.ToString("#,##0") + " €";
                            if (decimalPrice < _lastCostMin)
                                continue;
                            if (decimalPrice > _lastCostMax)
                                continue;
                        }
                    }
                    var description = wrapper.ElementsByClass("div", "description")?.FirstOrDefault()?.InnerText;
                    description = Regex.Replace(description, @"\s+", "");
                    var title = wrapper.ElementsByClass("div", "title")?.FirstOrDefault()?.InnerHtml;
                    title = Regex.Replace(title, @"\s+", "");
                    var href = SafeExtractHref(add);
                    var src = SafeExtractSrc(add);
                    if (src == null || src == " " || src == "")
                        src = "Images/noimage.png";
                    results.Add(new Advertisement
                    {
                        Name = title,
                        Description = description,
                        Price = price,
                        ImageUrl = src,
                        PageUrl = href,
                    });
                }
                bool hasMorePages = false;
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
                var pagination = document.GetElementById("pagination");
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
