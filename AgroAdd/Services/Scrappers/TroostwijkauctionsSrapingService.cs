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
    public class TroostwijkauctionsScrapingService : IScrapingService
    {
        private readonly LoggingService _loggingService;
        private WebBrowser _scrapBrowser;
        private bool _scrapDone;

        public string ServiceName => "Troostwijkauctions.com";
        public string Country => "EU";
        public bool IsAuction => true;
        public bool IsCompany => false;
        public bool RequiresText => true;

        public TroostwijkauctionsScrapingService(LoggingService loggingService)
        {
            _loggingService = loggingService;
        }

        public event ScrapCompleted AsyncScrapCompleted;

        public void ScrapAsync(string query, int? costmin, int? costmax, int page = 1)
        {
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
                    _scrapBrowser.Navigate($"https://www.troostwijkauctions.com/uk/search?s={query}");
                else
                    _scrapBrowser.Navigate($"https://www.troostwijkauctions.com/uk/search/{page}/?s={query}");
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, "Unhandled exception in ScrapTroostwijkauctionsWebBrowserAsync");
            }
        }

        private void ScrapBrowserLoadCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            if (_scrapDone || _scrapBrowser.ReadyState != WebBrowserReadyState.Complete)
                return;
            HtmlElementCollection adsli = null;
            var results = new List<Advertisement>();
            try
            {
                var ul = _scrapBrowser.Document?.ElementsByClass("ul", "lot-list").FirstOrDefault();
                if (ul == null)
                {
                    AsyncScrapCompleted?.Invoke(this, results, false, null);
                    return;
                }
                adsli = ul.GetElementsByTagName("li");
                if (adsli.Count == 0)
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
                foreach (HtmlElement add in adsli)
                {
                    var title = add.ElementsByClass("a", "title")?.FirstOrDefault()?.InnerText;
                    if (title == null)
                        continue;
                    var description = add.ElementsByClass("div", "lot-description")?.FirstOrDefault()?.InnerText;
                    var price = add.ElementsByClass("span", "time-parts")?.FirstOrDefault()?.InnerText;
                    var divSrc = add.ElementsByClass("div", "thumbnail")?.FirstOrDefault();
                    var src = SafeExtractSrc(divSrc);
                    if (src == null || src == " " || src == "")
                        src = "Images/noimage.png";
                    var href = SafeExtractHref(divSrc);

                    results.Add(new Advertisement
                    {
                        Name = title,
                        Description = description,
                        Price = price,
                        ImageUrl = src,
                        PageUrl = "https://www.troostwijkauctions.com" + href,
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
