using System.Collections.Generic;

namespace AgroAdd.Services.Scrappers.MarktplaatsScrapper.Models
{
    public class MarktplaatsListings
    {
        public string title { get; set; }
        public string description { get; set; }
        public MarktplaatsPriceInfo priceInfo { get; set; }
        public MarktplaatsLocation location { get; set; }
        public List<string> imageUrls { get; set; }
        public MarktplaatsSellerInformation sellerInformation { get; set; }
        public string vipUrl { get; set; }
    }
}