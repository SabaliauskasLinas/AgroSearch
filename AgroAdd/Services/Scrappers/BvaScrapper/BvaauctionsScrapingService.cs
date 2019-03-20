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
using Newtonsoft.Json;
using AgroAdd.Services.Scrappers.BvaScrapper.Models;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace AgroAdd.Services.Scrappers.BvaScrapper
{
    public class BvaauctionsScrapingService : IScrapingService
    {
        private readonly LoggingService _loggingService;
        private readonly HttpClient _httpClient;
        private Dispatcher _dispatcher;


        public string ServiceName => "Bva-Auctions.com";
        public string Country => "EU";
        public bool IsAuction => true;
        public bool IsCompany => false;
        public bool RequiresText => true;

        public BvaauctionsScrapingService(LoggingService loggingService)
        {
            _loggingService = loggingService;
            _httpClient = new HttpClient();
        }

        public event ScrapCompleted AsyncScrapCompleted;

        public void ScrapAsync(string query, int? costmin, int? costmax, int page = 1)
        {
            _dispatcher =  Dispatcher.CurrentDispatcher;
            try
            {
                _httpClient.GetStringAsync($"https://api.bva-auctions.com/api/rest/search?facets=on&fq=lottopcategory:83&language=nl&pageNumber={page}&pageSize=50&term={query}")
                    .ContinueWith(x => ScrapBrowserLoadCompleted(x.Result, costmin));
                
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, "Unhandled exception in ScrapMascusWebBrowserAsync");
            }
        }

        private void ScrapBrowserLoadCompleted(string apiResponse, int? costMin)
        {
            try
            {
                
                var results = new List<Advertisement>();
                var parsedResponse = JsonConvert.DeserializeObject<BvaSearchResponse>(apiResponse);

                if (parsedResponse == null || !parsedResponse.lots.Any())
                {
                    _dispatcher.Invoke(() => {
                        AsyncScrapCompleted?.Invoke(this, results, false, null);
                    });
                    return;
                }

                foreach (var add in parsedResponse.lots)
                {
                    results.Add(new Advertisement
                    {
                        Name = add.title,
                        Description = add.description+ Environment.NewLine + add.startDate + Environment.NewLine + add.endDate,
                        Price = (add.latestBidAmount??0) + add.currencyCode,
                        ImageUrl = add.thumbnailUrl,
                        PageUrl = add.lotPageUrl,
                    });
                }
                _dispatcher.Invoke(() => {
                    AsyncScrapCompleted?.Invoke(this, results, false, null);
                });
                
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, "Unhandled exception in ScrapBrowserLoadCompleted");
                _dispatcher.Invoke(() => {
                    AsyncScrapCompleted?.Invoke(this, null, false, "Unhandled exception");
                });
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
