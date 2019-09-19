using System.Collections.Generic;

namespace AgroAdd.Services.Scrappers.MarktplaatsScrapper.Models
{
    public class MarktplaatsPageProps
    {
        public MarktplaatsSearchRequestAndResponse searchRequestAndResponse { get; set; }
        public int page { get; set; }
    }
}