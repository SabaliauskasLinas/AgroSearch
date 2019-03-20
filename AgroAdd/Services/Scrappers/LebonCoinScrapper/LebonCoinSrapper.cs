using AgroAdd.Interfaces;
using AgroAdd.Models;
using AgroAdd.Services.Scrappers.LebonCoinScrapper.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace AgroAdd.Services.Scrappers.LebonCoinScrapper
{
    public class LebonCoinSrapper : IScrapingService
    {
        private readonly LoggingService _loggingService;
        private readonly HttpClient _httpClient;
        private Dispatcher _dispatcher;

        public event ScrapCompleted AsyncScrapCompleted;

        public string ServiceName => "Leboncoin.fr";
        public string Country => "FR";
        public bool IsAuction => false;
        public bool IsCompany => false;
        public bool RequiresText => true;

        public LebonCoinSrapper(LoggingService loggingService)
        {
            _loggingService = loggingService;
            _httpClient = new HttpClient();
        }

        public void ScrapAsync(string query, int? costmin, int? costmax, int page = 1)
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            try
            {
                var request = new SearchParams();
                request.filter.keywords.text = query;
                request.filter.keywords.type = "subject";
                request.filter.enums.ad_type = new[] { "offer" };
                request.limit = 35;
                request.limitAlu = 3;
                var content = new StringContent(JsonConvert.SerializeObject(request));

                _httpClient.PostAsync("https://api.leboncoin.fr/finder/search", content)
                    .ContinueWith(x => ScrapBrowserLoadCompleted(x.Result, costmin));

            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, "Unhandled exception in ScrapMascusWebBrowserAsync");
            }
        }

        private void ScrapBrowserLoadCompleted(HttpResponseMessage result, int? costmin)
        {
            var results = new List<Advertisement>();

            if (!result.IsSuccessStatusCode)
            {
                _dispatcher.Invoke(() => {
                    AsyncScrapCompleted?.Invoke(this, results, false, null);
                });
                return;
            }

            
            var response = result.Content.ReadAsStringAsync().Result;
            return;
        }
    }
}
