using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgroAdd.Services.Scrappers.BvaScrapper.Models
{
    public class BvaLot
    {
        public long id { get; set; }
        public long auctionId { get; set; }
        public int bidCount {get;set;}
        public string currencyCode { get; set; }
        public string description { get; set; }
        public DateTime endDate { get; set; }
        public bool favorite { get; set; }
        public decimal? latestBidAmount { get; set; }
        public string lotPageUrl { get; set; }
        public string name { get; set; }
        public string startDate { get; set; }
        public string thumbnailUrl { get; set; }
        public string title { get; set; }
    }
}
