using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgroAdd.Services.Scrappers.TradeMachinesScrapper.Models
{
    public class TradeMachinesResultsState
    {
        public TradeMachinesContent content { get; set;}
        public TradeMachinesOriginalResponse _originalResponse { get; set; }
    }
}
