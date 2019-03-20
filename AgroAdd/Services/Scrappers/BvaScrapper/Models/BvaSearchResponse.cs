using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgroAdd.Services.Scrappers.BvaScrapper.Models
{
    public class BvaSearchResponse
    {
        public List<BvaLot> lots { get; set; }
        public BvaPageContext pageContext { get; set; }
    }
}
