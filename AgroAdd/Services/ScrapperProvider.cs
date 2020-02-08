using AgroAdd.Interfaces;
using AgroAdd.Services.Scrappers;
using AgroAdd.Services.Scrappers.BvaScrapper;
using AgroAdd.Services.Scrappers.LebonCoinScrapper;
using AgroAdd.Services.Scrappers.MarktplaatsScrapper;
using AgroAdd.Services.Scrappers.TradeMachinesScrapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgroAdd.Services
{
    public class ScrapperProvider
    {
        private LoggingService _loggingService;


        public ScrapperProvider(LoggingService loggingService, CurrencyApi currencyApy)
        {
            _loggingService = loggingService;
            ScrapingServices = new List<IScrapingService>()
            {
                new AgriaffairesScrapingService(loggingService),
                new AtcTraderScrapingService(loggingService),
                new BaywaboerseScrapingService(loggingService),
                new BlocketScrapingService(loggingService, currencyApy),
                //new EuropeagrocultureScrapingService(loggingService),
                new MarktplaatsScrapingService(loggingService),
                new MascusScrapingService(loggingService, currencyApy),
                new MaskinbladetScrapingService(loggingService, currencyApy),
                new SkelbiuScrapingService(loggingService),
                new TradeMachinesScrapingService(loggingService),
                //new TraktorPoolScrapingService(loggingService),
                //new TrattoriSupermarketScrapingService(loggingService),
                new LandwirtScrapingService(loggingService,currencyApy),
                new TrucksScrapingService(loggingService),
                //new BvaauctionsScrapingService(loggingService),
                new EpicauctionsScrapingService(loggingService),
                new KlaravikScrapingService(loggingService),
                //new TroostwijkauctionsScrapingService(loggingService),
                new PfeiferMachineryScrapingService(loggingService),
                new FarolTractorScrapingService(loggingService, currencyApy),
                new FarolForkliftsTelehandlersScrapingService(loggingService, currencyApy),
                new FarolCombinesForagersScrapingService(loggingService, currencyApy),
                new FarolCultivationPloughsScrapingService(loggingService, currencyApy),
                new FarolDrillsScrapingService(loggingService,currencyApy),
                new FarolHayGrassScrapingService(loggingService,currencyApy),
                new FarolStrawSpreadersFeedersScrapingService(loggingService,currencyApy)
            };
        }

        public List<IScrapingService> ScrapingServices { get; private set; }


    }
}
