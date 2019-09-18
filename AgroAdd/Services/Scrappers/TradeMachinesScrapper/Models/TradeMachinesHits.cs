namespace AgroAdd.Services.Scrappers.TradeMachinesScrapper.Models
{
    public class TradeMachinesHits
    {
        public bool hasImg { get; set; }
        public string imgId { get; set; }
        public int year { get; set; }
        public float price { get; set; }
        public bool hasPrice { get; set; }
        public TradeMachinesSeller seller { get; set; }
        public TradeMachinesLocation location { get; set; }
        public TradeMachinesProduct product { get; set; }
        public string objectID { get; set; }
        public TradeMachinesFr fr { get; set; }
    }
}