using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgroAdd.Services.Scrappers.LebonCoinScrapper.Models
{
    public class SearchParams
    {
        public SearchParams()
        {
            filter = new SearchFilter();
        }

        public SearchFilter filter { get; set; }
        public int limit { get; set; }
        public int limitAlu { get; set; }

    }
}
