using MarketData.API.Helpers;
using MarketData.API.Worker;
using ProfitDLLClient;

namespace MarketData.API.Services
{
    public class ProfitService
    {
        //private static Delegate _tradeCallback;

        public void Start()
        {
            var settings = ConfigurationLoader.Settings;
            //_tradeCallback = new TradeCallback(OnTrade);

            ProfitDLL.DLLInitializeMarketLogin(
                settings.ProfitDLL.ActivationKey, settings.ProfitDLL.User, settings.ProfitDLL.Password,
                OnState,
                OnTrade,
                OnDaily,
                OnPriceBook,
                OnOfferBook,
                OnHistoryTrade,
                OnProgress,
                OnTinyBook
            );

            ProfitDll.SubscribeTicker("WDOFUT");

            //ProfitDll.SubscribeOfferBook("WDOFUT", "F");
            //ProfitDll.SubscribePriceDepth(...);
        }

        private void OnTinyBook(string ticker, double price)
        {
            Console.WriteLine($"Received tiny book update: {ticker} - {price}");
            //throw new NotImplementedException();
        }

        private void OnProgress(string ticker, double price)
        {
            Console.WriteLine($"Received progress update: {ticker} - {price}");
            //throw new NotImplementedException();
        }

        private void OnHistoryTrade(string ticker, double price)
        {
            Console.WriteLine($"Received history trade update: {ticker} - {price}");
            //throw new NotImplementedException();
        }

        private void OnOfferBook(string ticker, double price)
        {
            Console.WriteLine($"Received offer book update: {ticker} - {price}");
            //throw new NotImplementedException();
        }


        private void OnPriceBook(string ticker, double price)
        {
            Console.WriteLine($"Received price book update: {ticker} - {price}");
            //throw new NotImplementedException();
        }

        private void OnDaily(string ticker, double price)
        {
            Console.WriteLine($"Received daily update: {ticker} - {price}");    
            //throw new NotImplementedException();
        }

        private void OnTrade(string ticker, double price)
        {
            Console.WriteLine($"Received trade update: {ticker} - {price}");
            //throw new NotImplementedException();
        }

        private void OnState(string ticker, double price)
        {
            Console.WriteLine($"Received state update: {ticker} - {price}");
            //throw new NotImplementedException();
        }
    }
}
