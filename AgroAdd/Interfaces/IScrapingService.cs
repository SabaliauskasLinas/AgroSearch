using AgroAdd.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgroAdd.Interfaces
{
    public delegate void ScrapCompleted(IScrapingService sender, IEnumerable<Advertisement> results, bool hasMoreResults, string error);

    public interface IScrapingService
    {
        string ServiceName { get; }
        string Country { get; }
        bool IsAuction { get; }
        bool IsCompany { get; }
        bool RequiresText { get; }
        event ScrapCompleted AsyncScrapCompleted;
        void ScrapAsync(string query, int? costmin, int? costmax, int page = 1);
    }
}
