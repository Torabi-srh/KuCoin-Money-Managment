using CryptoExchange.Net.CommonObjects;
using CryptoExchange.Net.Objects;
using Kucoin.Net.Clients;
using Kucoin.Net.Objects;
using Kucoin.Net.Objects.Models.Spot;
using Microsoft.Extensions.Logging;
using System.Timers;
using System.Windows.Forms;

namespace Trader
{
    public partial class mainForm : Form
    {
        List<KucoinAccount> _assets = new List<KucoinAccount>();
        List<KucoinAccount> _workingAssets = new List<KucoinAccount>();
        KucoinAccount _selectedAssets = new KucoinAccount();
        KucoinClient kucoinClient = new KucoinClient();
        Models.Trade trade = new Models.Trade();
        bool isSandbox = true;
        List<Order> ordersList = new List<Order>();
        public mainForm()
        {
            InitializeComponent();
            button2.BackColor = Color.RoyalBlue;
            timer1.Enabled = true;
            timer1.Stop();
            updateClient();
        }
        private void updateClient()
        {
            string apiKey = string.IsNullOrEmpty(textBox1.Text) ? "63e501e036d21f000195e3aa" : textBox1.Text;
            string apiSecret = string.IsNullOrEmpty(textBox2.Text) ? "5c250c6c-230e-4188-8787-e4564cd54285" : textBox2.Text;
            string apiPass = string.IsNullOrEmpty(textBox3.Text) ? "f!2f2f32@f23cd$svbt" : textBox1.Text;
            kucoinClient = new KucoinClient(new KucoinClientOptions()
            {
                ApiCredentials = new KucoinApiCredentials(apiKey, apiSecret, apiPass),
                LogLevel = LogLevel.Trace,
                FuturesApiOptions = new KucoinRestApiClientOptions
                {
                    ApiCredentials = new KucoinApiCredentials(apiKey, apiSecret, apiPass),
                    AutoTimestamp = false,
                    BaseAddress = (isSandbox) ? KucoinApiAddresses.TestNet.FuturesAddress : KucoinApiAddresses.Default.FuturesAddress
                },
                SpotApiOptions = new KucoinRestApiClientOptions
                {
                    ApiCredentials = new KucoinApiCredentials(apiKey, apiSecret, apiPass),
                    AutoTimestamp = false,
                    BaseAddress = (isSandbox) ? KucoinApiAddresses.TestNet.SpotAddress : KucoinApiAddresses.Default.SpotAddress
                }
            });
        }
        private async void Form1_Load(object sender, EventArgs e)
        {
            WebCallResult<IEnumerable<KucoinAccount>> accountData = await kucoinClient.SpotApi.Account.GetAccountsAsync();
            _assets = accountData.Data.ToList();
            foreach (var item in _assets)
            {
                listBox1.Items.Add($"نوع: {item.Asset} مقدار: {item.Total} مقدار مجاز: {item.Available}");
            }
            WebCallResult<IEnumerable<Ticker>> x = await kucoinClient.SpotApi.CommonSpotClient.GetTickersAsync();
            foreach (var i in x.Data)
            {
                comboBox1.Items.Add(i.Symbol);
            }
            button1.Enabled = false;
            amountInp.ReadOnly = true;
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            foreach (var item in _assets)
            {
                if (listBox1.SelectedItem?.ToString() == $"نوع: {item.Asset} مقدار: {item.Total} مقدار مجاز: {item.Available}")
                {
                    _selectedAssets = item;
                    assetInp.Text = item.Asset;
                    amountInp.Text = item.Available.ToString();
                    amountInp.ReadOnly = false;
                    button1.Enabled = true;
                    amountInp.Maximum = item.Available;
                    break;
                }
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(assetInp.Text)) return;
            _workingAssets.Clear();
            listBox2.Items.Clear();
            decimal val = decimal.TryParse(amountInp.Text, out val) ? val : 0;
            _workingAssets.Add(new KucoinAccount() { Asset = assetInp.Text, Available = val, Total = _selectedAssets.Total });
            listBox2.Items.Add($"نوع: {assetInp.Text} مقدار: {val}");
            assetInp.Text = "";
            amountInp.Text = "";
            amountInp.ReadOnly = true;
            button1.Enabled = false;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (button3.Text == "Sandbox")
            {
                button3.Text = "Real !";
                button3.BackColor = Color.DarkGreen;
                isSandbox = false;
            }
            else
            {
                button3.Text = "Sandbox";
                button3.BackColor = Color.Maroon;
                isSandbox = true;
            }

            updateClient();
        }
        private async void button2_Click(object sender, EventArgs e)
        {
            if (button2.Text == "پایان معامله")
            {
                timer1.Stop();
                button2.Text = "شروع معامله";
                button2.BackColor = Color.Maroon;
                ordersList.Clear();
            }
            else
            {
                int intval = int.TryParse(interval.Text, out intval) ? intval : 1;
                timer1.Interval = intval * 1000;
                button2.Text = "پایان معامله";
                button2.BackColor = Color.RoyalBlue;
                string prompt = $"{DateTime.Now} \n";
                foreach (var item in _workingAssets)
                {
                    WebCallResult<Ticker> x = await kucoinClient.SpotApi.CommonSpotClient.GetTickerAsync(comboBox1.Text);
                    decimal price = x.Data?.LastPrice ?? 0;
                    decimal tradeParts = Math.Round(item.Available / division.Value);
                    decimal rebuy = price * rebuyPersentage.Value / 100;
                    decimal close = price * closePersantage.Value / 100;
                    string closeStr = checkBox1.Checked ? " خروج کامل " : " سیو سود ";
                    string closeStrfull = checkBox1.Checked ? "فعال" : "غیر فعال";
                    prompt += $"انجام معامله از روی {item.Asset} \n" +
                              $"انجام معامله برای {comboBox1.Text} \n" +
                              $"به مقدار {item.Available} واحد\n" +
                              $"به تعداد {division.Value} قسمت\n" +
                              $"هر قسمت معادله {tradeParts} واحد\n" +
                              $"خرید مجدد در ~{rebuy} \n" +
                              $"{closeStr} در ~{close} \n" +
                              $"خروج کام: {closeStrfull} \n" +
                              $"قیمت در این لحظه {price} \n";
                    trade.lastPrice = price;
                    trade.startingCurrency = item.Available;
                    trade.reBuyAt = rebuy;
                    trade.reBuyAtP = rebuyPersentage.Value;
                    trade.closeAt = close;
                    trade.closeAtP = closePersantage.Value;
                    trade.fullClose = checkBox1.Checked;
                    trade.symbol = comboBox1.Text;
                    trade.chonks = (int)division.Value;
                    trade.tradeParts = tradeParts;
                }
                Preview prv = new Preview(prompt);
                prv.StartPosition = FormStartPosition.CenterScreen;
                if (prv.ShowDialog() == DialogResult.OK)
                {
                    timer1.Start();
                }
            }
        }
        private async void open()
        {
            WebCallResult<KucoinNewOrder> x = await kucoinClient.SpotApi.Trading.PlaceOrderAsync(trade.symbol, Kucoin.Net.Enums.OrderSide.Buy, Kucoin.Net.Enums.NewOrderType.Market
                , trade.tradeParts);
            if (!x.Success)
            {
                open();
            }
            WebCallResult<Order> t = await kucoinClient.SpotApi.CommonSpotClient.GetOrderAsync(x.Data.Id);
            ordersList.Add(t.Data);
            trade.lastPrice = t.Data.Price ?? 0;
            listBox3.Items.Add($"سفارش با شماره {x.Data.Id} به حجم {trade.tradeParts} بر روی {trade.symbol} ثبت شد.");
        }
        private async void close()
        {
            WebCallResult<KucoinCanceledOrders> x = await kucoinClient.SpotApi.Trading.CancelAllOrdersAsync();
            if (!x.Success)
            {
                close();
            }
            foreach (string order in x.Data.CancelledOrderIds)
            {
                listBox3.Items.Add($"سفارش با شماره {order} بسته شد.");
            }
            button2_Click(new object(), new EventArgs());
        }
        private async void timer1_Tick(object sender, EventArgs e)
        {
            WebCallResult<Ticker> x = await kucoinClient.SpotApi.CommonSpotClient.GetTickerAsync(trade.symbol);
            if (ordersList.Count < 1) open();
            if (x.Data.LastPrice <= ordersList.First().Price * trade.reBuyAtP / 100) open();
            if (x.Data.LastPrice >= ordersList.First().Price * trade.closeAtP / 100) close();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            updateClient();
        }
    }
}