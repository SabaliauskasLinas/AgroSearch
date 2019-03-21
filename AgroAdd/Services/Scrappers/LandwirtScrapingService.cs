﻿using AgroAdd.Interfaces;
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
    public class LandwirtScrapingService : IScrapingService
    {
        private readonly LoggingService _loggingService;
        private readonly CurrencyApi _currencyApi;
        private WebBrowser _scrapBrowser;
        private int? _lastCostMin;
        private int? _lastCostMax;
        private bool _scrapDone;
        private int _offset;
        private decimal _currentRate = 1.15206m;
        private bool _rateLoaded;

        public string ServiceName => "Landwirt.com";
        public string Country => "UK";
        public bool IsAuction => false;
        public bool IsCompany => false;
        public bool RequiresText => true;

        public LandwirtScrapingService(LoggingService loggingService,CurrencyApi currencyApi)
        {
            _loggingService = loggingService;
            _currencyApi = currencyApi;
        }

        public event ScrapCompleted AsyncScrapCompleted;


        public void ScrapAsync(string query, string filters, int? costmin, int? costmax, int page = 1)
        {
            _lastCostMin = costmin;
            _lastCostMax = costmax;
            _scrapDone = false;
            _offset = 0;
            if (page != 1)
                _offset = _offset + 20 * (page - 1);
            try
            {
                if (_scrapBrowser == null)
                {
                    _scrapBrowser = new WebBrowser();
                    _scrapBrowser.DocumentCompleted += ScrapBrowserLoadCompleted;
                    _scrapBrowser.ScriptErrorsSuppressed = true;
                }
                if (page < 2)
                    _scrapBrowser.Navigate($"https://www.landwirt.com/used-farm-machinery/en/index.php?action=search&new=1&msearch=1&q={query}&catIdFulltext=0");
                else
                    _scrapBrowser.Navigate($"https://www.landwirt.com/used-farm-machinery/en/index.php?action=search&offset={_offset}");
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
                _loggingService.LogException(ex, "Unhandled exception in ScrapAtcTraderWebBrowserAsync");
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

            IEnumerable<HtmlElement> ads = null;
            var results = new List<Advertisement>();
            try
            {
                var adsParent = _scrapBrowser.Document.GetElementById("treffertabelle");
                ads = adsParent?.ElementsByClass("div","gmmtreffer");
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
                    var info = add.ElementsByClass("div", "gmmtreffertext")?.FirstOrDefault();
                    if (info == null)
                        continue;
                    var title = info.ElementsByClass("div", "h3")?.FirstOrDefault()?.InnerText;
                    var allDescriptionsCount = info.ElementsByClass("div", "gmmlistcatfield").Count();
                    string description = "";
                    while (allDescriptionsCount != 0)
                    {
                        description = description + info?.ElementsByClass("div", "gmmlistcatfield").ElementAt(allDescriptionsCount - 1).InnerText + "\n";
                        allDescriptionsCount--;
                    }
                    var price = add?.ElementsByClass("span", "pricetagbig")?.FirstOrDefault()?.InnerText;
                    if (price == null)
                        price = "POA";
                    else
                    {
                        price = price.Replace(",", "").Replace("-", "").Replace(".", "").Replace("GBP","").Replace(" ","");
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
                    var divImg = add.ElementsByClass("div", "col-xs-4")?.FirstOrDefault();
                    var src = SafeExtractSrc(divImg);
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

        }
        private bool ScrapHasMorePages(HtmlDocument document)
        {
            try
            {
                var pagination = document.ElementsByClass("ul", "pagination")?.FirstOrDefault();
                var lisCount = pagination.GetElementsByTagName("li").Count;
                var lis = pagination?.GetElementsByTagName("li");
                while (lisCount != 0)
                {
                    if (lis[lisCount - 1]?.InnerText == "»")
                        return true;
                    lisCount--;
                }
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
            return el[0].GetAttribute("data-original");
        }
    }
}
