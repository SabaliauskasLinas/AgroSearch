using System.Collections.Generic;

namespace AgroAdd.Services.Scrappers.MarktplaatsScrapper.Models
{
    public class MarktplaatsSearchRequestAndResponse
    {
        public List<MarktplaatsListings> listings { get; set; }
        public int totalResultCount { get; set; }
    }
}