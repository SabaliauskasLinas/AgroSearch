namespace AgroAdd.Services.Scrappers.TradeMachinesScrapper.Models
{
    public class TradeMachinesHits
    {
        public bool hasImg { get; set; }
        public string imgId { get; set; }
        public int year { get; set; }
        public int price { get; set; }
        public bool hasPrice { get; set; }
        public TradeMachinesLocation location { get; set; }
        public TradeMachinesProduct product { get; set; }
        public string objectID { get; set; }
    }
}