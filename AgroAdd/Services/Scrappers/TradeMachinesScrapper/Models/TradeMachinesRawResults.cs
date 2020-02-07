using System.Collections.Generic;

namespace AgroAdd.Services.Scrappers.TradeMachinesScrapper.Models
{
    public class TradeMachinesRawResults
    {
        public List<TradeMachinesHits> hits { get; set; }
        public int nbHits { get; set; }
        public int page { get; set; }
        public int nbPages { get; set; }
        public int hitsPerPage { get; set; }
    }
}