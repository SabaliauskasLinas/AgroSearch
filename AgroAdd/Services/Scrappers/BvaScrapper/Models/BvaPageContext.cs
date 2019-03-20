using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgroAdd.Services.Scrappers.BvaScrapper.Models
{
    public class BvaPageContext
    {
        public bool hasNext { get; set; }
        public int pageNumber { get; set; }
        public int pageSize { get; set; }
        public int totalSize { get; set; }
    }
}
