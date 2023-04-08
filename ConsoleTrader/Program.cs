using ConsoleTrader.Models;
using CryptoExchange.Net.Objects;
using Kucoin.Net.Clients;
using Kucoin.Net.Objects;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
namespace ConsoleTrader
{
    internal class Program
    {
        static Trader currentTrader = new Trader();
        static KucoinClient kucoinClient = new KucoinClient();
        static Models.Trade? trade = new Models.Trade();
        static decimal currentBalance = 0;
        static Calclist Calculate = new Calclist();
        static bool isSandbox = true;
        static Queue<Calclist> tradeCalc = new Queue<Calclist>();
        static Dictionary<string, decimal> placedOrdersList = new Dictionary<string, decimal>();
        static List<decimal> avgList = new List<decimal>();
        static string Pair;

        static bool first = false;
        static void Main(string[] args)
        {
            string jsonString = File.ReadAllText("settings.json");
            trade = JsonConvert.DeserializeObject<Models.Trade>(jsonString);
            foreach (var item in trade.calcList)
            {
                tradeCalc.Enqueue(item);
            }
            isSandbox = trade.sandbox == 0 ? false : true;
            kucoinClient = new KucoinClient(new KucoinClientOptions()
            {
                ApiCredentials = new KucoinApiCredentials(trade.apiKey, trade.apiSecret, trade.apiPassphrase),
                LogLevel = LogLevel.Trace,
                FuturesApiOptions = new KucoinRestApiClientOptions
                {
                    ApiCredentials = new KucoinApiCredentials(trade.apiKey, trade.apiSecret, trade.apiPassphrase),
                    AutoTimestamp = false,
                    BaseAddress = (isSandbox) ? KucoinApiAddresses.TestNet.FuturesAddress : KucoinApiAddresses.Default.FuturesAddress
                },
                SpotApiOptions = new KucoinRestApiClientOptions
                {
                    ApiCredentials = new KucoinApiCredentials(trade.apiKey, trade.apiSecret, trade.apiPassphrase),
                    AutoTimestamp = false,
                    BaseAddress = (isSandbox) ? KucoinApiAddresses.TestNet.SpotAddress : KucoinApiAddresses.Default.SpotAddress
                }
            });
            updateClient().Wait();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
        static async Task LimitTrader()
        {
            avgList.Clear();
            int divs = currentTrader.divides;
            Queue<Calclist> tradeCalcs = tradeCalc;
            Calculate = tradeCalcs.Dequeue();
            decimal quantity = Math.Round(currentTrader.quantity, 4);
            var orderData = await kucoinClient.SpotApi.Trading.PlaceOrderAsync(currentTrader.symbol,
                Kucoin.Net.Enums.OrderSide.Buy, Kucoin.Net.Enums.NewOrderType.Market, quoteQuantity: quantity);
            Console.WriteLine($"Trade Market: Buy Quantity: {quantity}");
            Console.WriteLine("");

            var t = await kucoinClient.SpotApi.CommonSpotClient.GetOrderAsync(orderData.Data.Id);
            var _data = await kucoinClient.SpotApi.CommonSpotClient.GetTickerAsync(currentTrader.symbol);
            var price = _data.Data.LastPrice ?? 0;
            avgList.Add(price);

            decimal rebuyPrice = Math.Abs(price  - (Calculate.down * price / 100));
            decimal sellPrice = price + (Calculate.up * price / 100);
            rebuyPrice = Math.Round(rebuyPrice, 4);
            sellPrice = Math.Round(sellPrice, 4);

            var orderDataLimiteSell = await kucoinClient.SpotApi.Trading.PlaceOrderAsync(currentTrader.symbol,
                      Kucoin.Net.Enums.OrderSide.Sell, Kucoin.Net.Enums.NewOrderType.Limit,
                      quantity: t.Data.QuantityFilled ?? 0, price: sellPrice);
            var orderDataLimiteBuy = await kucoinClient.SpotApi.Trading.PlaceOrderAsync(currentTrader.symbol, Kucoin.Net.Enums.OrderSide.Buy,
                                           Kucoin.Net.Enums.NewOrderType.Limit, quantity: t.Data.QuantityFilled ?? 0, price: rebuyPrice);
            Console.WriteLine($"Trade 1: Sell Type Limit Quantity: {t.Data.QuantityFilled} Price {sellPrice}");
            Console.WriteLine($"---------------------------------------");
            Console.WriteLine($"Trade 1: Buy Type Limit Quantity: {t.Data.QuantityFilled} Price {rebuyPrice}");
            Console.WriteLine("");
            avgList.Add(rebuyPrice);
            decimal iQuantity = t.Data?.QuantityFilled ?? 0;
            Calculate = tradeCalcs.Dequeue();
            int ind = 1;
            while (divs > 0)
            {
                divs--;
                currentTrader.avgPrice = 0;
                foreach (var bought in avgList)
                {
                    if (bought == 0) continue;
                    currentTrader.avgPrice += bought;
                }
                currentTrader.avgPrice /= avgList.Count;

                sellPrice = currentTrader.avgPrice + (Calculate.up * currentTrader.avgPrice / 100);
                rebuyPrice = Math.Abs(rebuyPrice - (Calculate.down * rebuyPrice / 100));

                rebuyPrice = Math.Round(rebuyPrice, 4);
                sellPrice = Math.Round(sellPrice, 4);

                placedOrdersList.Add(orderDataLimiteBuy.Data.Id, sellPrice);
                orderDataLimiteBuy = await kucoinClient.SpotApi.Trading.PlaceOrderAsync(currentTrader.symbol, Kucoin.Net.Enums.OrderSide.Buy,
                                             Kucoin.Net.Enums.NewOrderType.Limit, quantity: t.Data.QuantityFilled ?? 0, price: rebuyPrice);

                ind++;
                Console.WriteLine($"Trade {ind}: Sell Type Limit Quantity: {t.Data.QuantityFilled} Price {sellPrice}");
                Console.WriteLine($"---------------------------------------");
                Console.WriteLine($"Trade {ind}: Buy Type Limit Quantity: {t.Data.QuantityFilled} Price {rebuyPrice}");
                Console.WriteLine("");

                avgList.Add(rebuyPrice);

                if (tradeCalcs.Count == 0) break;
                if (tradeCalcs.Count > 0) Calculate = tradeCalcs.Dequeue();
            }

            if (tradeCalc.Count < 1)
            {
                foreach (var item in trade.calcList)
                {
                    tradeCalc.Enqueue(item);
                }
            }
            while (true)
            {
                var ordersList = await kucoinClient.SpotApi.Trading.GetOrdersAsync(currentTrader.symbol, Kucoin.Net.Enums.OrderSide.Sell, status: Kucoin.Net.Enums.OrderStatus.Done);
                if (ordersList.Success)
                {
                    if (ordersList.Data.Items.Where(x => x.IsActive == true).ToList().Count == 0)
                    {
                        var ordersCancelAll = await kucoinClient.SpotApi.Trading.CancelAllOrdersAsync(currentTrader.symbol);

                        var _assets = await kucoinClient.SpotApi.Account.GetAccountsAsync();
                        var tx = _assets.Data.Where(x => x.Asset == currentTrader.symbol.Split("-")[0]).FirstOrDefault();
                        if (tx?.Available != null)
                        {
                            if (tx.Available > 0.00001m)
                            {
                                var qtn = sellPrice = Math.Round(tx.Available, 4);
                                orderData = await kucoinClient.SpotApi.Trading.PlaceOrderAsync(currentTrader.symbol,
                                                Kucoin.Net.Enums.OrderSide.Sell, Kucoin.Net.Enums.NewOrderType.Market, quoteQuantity: qtn);
                                if (orderData?.Data == null)
                                {
                                    _ = orderData;
                                }
                            }
                        }
                        Console.WriteLine($"Trade done");
                        if (trade?.manual != null && trade.manual == 0)
                        {
                            await LimitTrader();
                        }
                        return;
                    } 
                    foreach (var item in placedOrdersList)
                    {
                        if (ordersList.Data.Items.Where(x => x.IsActive == true && x.Id == item.Key).ToArray().Count() == 0)
                        {
                            _ = await kucoinClient.SpotApi.Trading.CancelOrderAsync(orderDataLimiteSell.Data.Id);
                            iQuantity += ordersList.Data?.Items?.Where(x => x.IsActive == true && x.Id == item.Key)?.FirstOrDefault()?.QuantityFilled ?? 0;
                            orderDataLimiteSell = await kucoinClient.SpotApi.Trading.PlaceOrderAsync(currentTrader.symbol,
                                                    Kucoin.Net.Enums.OrderSide.Sell, Kucoin.Net.Enums.NewOrderType.Limit,
                                                    quantity: iQuantity, price: item.Value);
                            placedOrdersList.Remove(item.Key);
                            Console.WriteLine($"Sell order updated with price {item.Value} and quantity of total {iQuantity}");
                            break;
                        }
                    }

                }
            }
        }
        static async Task LimitTraderReporter()
        {
            int divs = currentTrader.divides;
            Queue<Calclist> tradeCalcs = tradeCalc;
            List<decimal> avgListTmp = new List<decimal>();
            Calculate = tradeCalcs.Dequeue();
            decimal quantity = Math.Round(currentTrader.quantity, 4);
            Console.WriteLine("Trade List Report: ");
            Console.WriteLine($"Trade Start: Buy Type Market Quantity: {quantity} ");
             
            var _data = await kucoinClient.SpotApi.CommonSpotClient.GetTickerAsync(currentTrader.symbol);
            var price = _data.Data.LastPrice ?? 0;
            avgListTmp.Add(price);

            decimal rebuyPrice = Math.Abs(price - (Calculate.down * price / 100));
            decimal sellPrice = price + (Calculate.up * price / 100);

            rebuyPrice = Math.Round(rebuyPrice, 10);
            sellPrice = Math.Round(sellPrice, 10);

            Console.WriteLine($"Trade 1: Sell Type Limit Quantity: {quantity} Price {sellPrice}");
            Console.WriteLine($"Trade 1: Buy Type Limit Quantity: {quantity} Price {rebuyPrice}");

            avgListTmp.Add(rebuyPrice);
            Calculate = tradeCalcs.Dequeue();
            int ind = 1;
            while (divs > 0)
            {
                ind++;
                divs--;
                currentTrader.avgPrice = 0;
                foreach (var bought in avgListTmp)
                {
                    currentTrader.avgPrice += bought;
                }
                currentTrader.avgPrice /= avgListTmp.Count;


                sellPrice = currentTrader.avgPrice + (Calculate.up * currentTrader.avgPrice / 100);
                rebuyPrice = Math.Abs(rebuyPrice - (Calculate.down * rebuyPrice / 100));

                rebuyPrice = Math.Round(rebuyPrice, 10);
                sellPrice = Math.Round(sellPrice, 10);
                Console.WriteLine($"Trade {ind}: Sell Type Limit Quantity: {quantity} Price {sellPrice}");
                Console.WriteLine($"Trade {ind}: Buy Type Limit Quantity: {quantity} Price {rebuyPrice}");
                Console.WriteLine("");
                avgListTmp.Add(rebuyPrice);
                if (tradeCalcs.Count <= 0) break;
                Calculate = tradeCalcs.Dequeue();
            }

            if (tradeCalc.Count < 1)
            {
                foreach (var item in trade.calcList)
                {
                    tradeCalc.Enqueue(item);
                }
            }
        }

        static async Task updateClient()
        {
            if (trade is null) return;
            Console.Clear();
            var _assets = await kucoinClient.SpotApi.Account.GetAccountsAsync();
            int ci = 1;
            foreach (var item in _assets.Data)
            {
                Console.WriteLine($"{ci}. Asset: {item.Asset} Total: {item.Total} Balance: {item.Available}");
                ci++;
            }
            Console.WriteLine($"Select Asset to continue: ");
            int key = int.TryParse(Console.ReadLine(), out key) ? key : -1;
            while (key == -1 || key > _assets.Data.Count())
            {
                Console.WriteLine($"Wrong number");
                Console.WriteLine($"Again: ");
                key = int.TryParse(Console.ReadLine(), out key) ? key : -1;
            }
            var _selectedAssets = _assets.Data.ToArray()[key - 1];
            currentBalance = (_selectedAssets.Available * trade.balance) / 100;
            var _tmp = await kucoinClient.SpotApi.ExchangeData.GetSymbolsAsync();
            var _tickers = _tmp.Data.Where(x => x.Name.EndsWith(_selectedAssets.Asset)).ToArray();
            ci = 1;
            foreach (var i in _tickers)
            {
                Console.WriteLine($"{ci}. {i.Name}");
                ci++;
            }

            Console.WriteLine($"Select Symbol to continue: ");
            key = int.TryParse(Console.ReadLine(), out key) ? key : -1;
            while (key == -1 || key > _tickers.Count())
            {
                Console.WriteLine($"Wrong number");
                Console.WriteLine($"Again: ");
                key = int.TryParse(Console.ReadLine(), out key) ? key : -1;
            }
            var _selectedTickers = (key == 0) ? trade.pair : _tickers[key - 1].Name;

            Pair = _selectedTickers;
            trade.interval = trade.interval * 1000;
            string prompt = $"{DateTime.Now} \n";
            WebCallResult<CryptoExchange.Net.CommonObjects.Ticker> xticker = await kucoinClient.SpotApi.CommonSpotClient.GetTickerAsync(_selectedTickers);
            decimal price = xticker.Data?.LastPrice ?? 0;
            Calculate = tradeCalc.Dequeue();
            tradeCalc.Enqueue(Calculate);
            currentTrader.down = Calculate.down;
            currentTrader.up = Calculate.up;
            currentTrader.symbol = _selectedTickers;
            currentTrader.quantity = currentBalance / trade.divides;
            currentTrader.divides = trade.divides;

            prompt += $"Trade With {_selectedAssets.Asset} \n" +
                      $"Buy {_selectedTickers} \n" +
                      $"Total used balance {_selectedAssets.Available} (unit)\n" +
                      $"Divided by {trade.divides} parts\n" +
                      $"Quantity {currentBalance} (unit)\n" +
                      $"Stop rebuy at ~{Calculate.down} \n" +
                      $"Close all at ~{Calculate.up} \n" +
                      $"Estimated Price {price} \n";
            Console.Clear();
            Console.WriteLine(prompt);
            Console.WriteLine("");
            await LimitTraderReporter();
            Console.WriteLine($"Do you continue (yes, no, y, n)? ");
            string? k = Console.ReadLine();
            while (k?.ToLower() == "y" || k?.ToLower() == "yes")
            {
                Console.Clear();
                //await startTrader();
                await LimitTrader();
            }
        }
    }
}