using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace AgroAdd.Services
{
    public class CurrencyApi
    {
        private HttpClient _httpClient { get; }
        private LoggingService _loggingService { get; }
        
        public CurrencyApi(LoggingService loggingService)
        {
            _httpClient = new HttpClient();
            _loggingService = loggingService;
        }

        public decimal GetRate(string currencyCode)
        {
            try
            {
                var result = _httpClient.GetStringAsync($"https://free.currencyconverterapi.com/api/v6/convert?q={currencyCode}_EUR&compact=y&apiKey=2f2ee51709f4b5ad1074").Result;
                var jObject = JObject.Parse(result);
                return jObject.GetValue($"{currencyCode}_EUR").Value<decimal>("val");
            }
            catch(Exception ex)
            {
                _loggingService.LogException(ex, "Unhandled exception while downloading the exchange rate");
                return 0m;
            }
        }
        public async Task<decimal> GetRateAsync(string currencyCode)
        {
            try
            {
                var result = await _httpClient.GetStringAsync($"https://free.currencyconverterapi.com/api/v6/convert?q={currencyCode}_EUR&compact=y&apiKey=2f2ee51709f4b5ad1074");
                var jObject = JObject.Parse(result);
                return jObject.GetValue($"{currencyCode}_EUR").Value<decimal>("val");
            }
            catch (Exception ex)
            {
                _loggingService.LogException(ex, "Unhandled exception while downloading the exchange rate");
                return 0m;
            }
        }


    }
}
