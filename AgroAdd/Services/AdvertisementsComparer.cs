using AgroAdd.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgroAdd.Services
{
    public class AdvertisementsComparer : IComparer<AdvertisementViewModel>
    {

        public int Compare(AdvertisementViewModel modelA, AdvertisementViewModel modelB)
        {
            modelA.Price = modelA.Price.Replace(",", "").Replace(".", "").Replace("€", "").Replace(" ","");
            if (!decimal.TryParse(modelA.Price, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal priceA))
                priceA = decimal.MaxValue;
            modelB.Price = modelB.Price.Replace(",", "").Replace(".", "").Replace("€", "").Replace(" ", "");
            if (!decimal.TryParse(modelB.Price, NumberStyles.Any, NumberFormatInfo.InvariantInfo, out decimal priceB))
                priceB = decimal.MaxValue;
            
            if (priceA!=decimal.MaxValue)
                modelA.Price = priceA.ToString("### ###") + " €";
            if (priceB!=decimal.MaxValue)
                modelB.Price = priceB.ToString("### ###") + " €";

            if (priceA < priceB)
                return -1;
            if (priceA > priceB)
                return 1;
            return 0;
        }
    }
}
