using System.Collections.Generic;

namespace AgroAdd.Services.Scrappers.MarktplaatsScrapper.Models
{
    public class MarktplaatsPageProps
    {
        public List<MarktplaatsListings> listings { get; set; }
        public int totalResultCount { get; set; }
        public int page { get; set; }
    }
}