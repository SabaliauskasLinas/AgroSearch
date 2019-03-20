using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgroAdd.Services.Scrappers.LebonCoinScrapper.Models
{
    public class SearchFilter
    {
        public SearchFilter()
        {
            keywords = new SearchKeyWord();
            enums = new SearchEnumcs();
        }
        public SearchKeyWord keywords { get; set; }
        public SearchEnumcs enums { get; set; }

    }
}
