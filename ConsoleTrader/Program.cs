﻿using ConsoleTrader.Models;
using KuCoinApi.Net;
using KuCoinApi.Net.Entities;
using Microsoft.VisualBasic;
using CryptoExchange.Net.CommonObjects;
using CryptoExchange.Net.Objects;
using Kucoin.Net.Clients;
using Kucoin.Net.Objects;
using Kucoin.Net.Objects.Models.Spot;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Drawing;
using System.Linq;
using System.Xml;
using Order = KuCoinApi.Net.Entities.Order;
//limite order faqat besazim
namespace ConsoleTrader
{
    internal class Program
    {
        static Trader currentTrader = new Trader();
        static KuCoinDotNet kucoinClient = new KuCoinDotNet();
        static KucoinClient kucoinClient2 = new KucoinClient();
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
            kucoinClient2 = new KucoinClient(new KucoinClientOptions()
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
            kucoinClient = new KuCoinDotNet(trade.apiKey, trade.apiSecret, trade.apiPassphrase, isSandbox);
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
            var orderData = await kucoinClient2.SpotApi.Trading.PlaceOrderAsync(currentTrader.symbol,
                Kucoin.Net.Enums.OrderSide.Buy, Kucoin.Net.Enums.NewOrderType.Market, quoteQuantity: quantity);
            Console.WriteLine($"Trade Market: Buy Quantity: {quantity}");
            Console.WriteLine("");

            var t = await kucoinClient2.SpotApi.CommonSpotClient.GetOrderAsync(orderData.Data.Id);
            var _data = await kucoinClient.GetTicker(currentTrader.symbol);
            avgList.Add(_data.Price);

            decimal rebuyPrice = Math.Abs(_data.Price - (Calculate.down * _data.Price / 100));
            decimal sellPrice = _data.Price + (Calculate.up * _data.Price / 100);
            rebuyPrice = Math.Round(rebuyPrice, 4);
            sellPrice = Math.Round(sellPrice, 4);

            var orderDataLimiteSell = await kucoinClient2.SpotApi.Trading.PlaceOrderAsync(currentTrader.symbol,
                      Kucoin.Net.Enums.OrderSide.Sell, Kucoin.Net.Enums.NewOrderType.Limit,
                      quantity: t.Data.QuantityFilled ?? 0, price: sellPrice);
            var orderDataLimiteBuy = await kucoinClient2.SpotApi.Trading.PlaceOrderAsync(currentTrader.symbol, Kucoin.Net.Enums.OrderSide.Buy,
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
                    currentTrader.avgPrice += bought;
                }
                currentTrader.avgPrice /= avgList.Count;

                sellPrice = currentTrader.avgPrice + (Calculate.up * currentTrader.avgPrice / 100);
                rebuyPrice = Math.Abs(rebuyPrice - (Calculate.down * rebuyPrice / 100));

                rebuyPrice = Math.Round(rebuyPrice, 4);
                sellPrice = Math.Round(sellPrice, 4);

                placedOrdersList.Add(orderDataLimiteBuy.Data.Id, sellPrice);
                orderDataLimiteBuy = await kucoinClient2.SpotApi.Trading.PlaceOrderAsync(currentTrader.symbol, Kucoin.Net.Enums.OrderSide.Buy,
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
                var ordersList = await kucoinClient2.SpotApi.Trading.GetOrdersAsync(currentTrader.symbol, Kucoin.Net.Enums.OrderSide.Sell, status: Kucoin.Net.Enums.OrderStatus.Done);
                if (ordersList.Success)
                {
                    if (ordersList.Data.Items.Where(x => x.IsActive == true).ToList().Count == 0)
                    {
                        var ordersCancelAll = await kucoinClient2.SpotApi.Trading.CancelAllOrdersAsync(currentTrader.symbol);

                        var _assets = await kucoinClient.GetBalances();
                        var tx = _assets.Where(x => x.Symbol == currentTrader.symbol.Split("-")[0]).FirstOrDefault();
                        if (tx?.Available != null)
                        {
                            if (tx.Available > 0.00001m)
                            {
                                var qtn = sellPrice = Math.Round(tx.Available, 4);
                                orderData = await kucoinClient2.SpotApi.Trading.PlaceOrderAsync(currentTrader.symbol,
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
                            _ = await kucoinClient2.SpotApi.Trading.CancelOrderAsync(orderDataLimiteSell.Data.Id);
                            iQuantity += ordersList.Data?.Items?.Where(x => x.IsActive == true && x.Id == item.Key)?.FirstOrDefault()?.QuantityFilled ?? 0;
                            orderDataLimiteSell = await kucoinClient2.SpotApi.Trading.PlaceOrderAsync(currentTrader.symbol,
                                                    Kucoin.Net.Enums.OrderSide.Sell, Kucoin.Net.Enums.NewOrderType.Limit,
                                                    quantity: iQuantity, price: item.Value);
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

            var _data = await kucoinClient.GetTicker(currentTrader.symbol);
            avgListTmp.Add(_data.Price);

            decimal rebuyPrice = Math.Abs(_data.Price - (Calculate.down * _data.Price / 100));
            decimal sellPrice = _data.Price + (Calculate.up * _data.Price / 100);

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
            var _assets = await kucoinClient.GetBalances();
            int ci = 1;
            foreach (var item in _assets)
            {
                Console.WriteLine($"{ci}. Asset: {item.Symbol} Total: {item.Total} Balance: {item.Available}");
                ci++;
            }
            Console.WriteLine($"Select Asset to continue: ");
            int key = int.TryParse(Console.ReadLine(), out key) ? key : -1;
            while (key == -1 || key > _assets.Count())
            {
                Console.WriteLine($"Wrong number");
                Console.WriteLine($"Again: ");
                key = int.TryParse(Console.ReadLine(), out key) ? key : -1;
            }
            var _selectedAssets = _assets[key - 1];
            currentBalance = (_selectedAssets.Available * trade.balance) / 100;
            var _tmp = await kucoinClient.GetTradingPairs();
            var _tickers = _tmp.Where(x => x.EndsWith(_selectedAssets.Symbol)).ToList();
            ci = 1;
            foreach (var i in _tickers)
            {
                Console.WriteLine($"{ci}. {i}");
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
            var _selectedTickers = (key == 0) ? trade.pair : _tickers[key - 1];

            Pair = _selectedTickers;
            trade.interval = trade.interval * 1000;
            string prompt = $"{DateTime.Now} \n";
            WebCallResult<CryptoExchange.Net.CommonObjects.Ticker> xticker = await kucoinClient2.SpotApi.CommonSpotClient.GetTickerAsync(_selectedTickers);
            decimal price = xticker.Data?.LastPrice ?? 0;
            Calculate = tradeCalc.Dequeue();
            tradeCalc.Enqueue(Calculate);
            currentTrader.down = Calculate.down;
            currentTrader.up = Calculate.up;
            currentTrader.symbol = _selectedTickers;
            currentTrader.quantity = currentBalance / trade.divides;
            currentTrader.divides = trade.divides;

            prompt += $"Trade With {_selectedAssets.Symbol} \n" +
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