using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Trader.Models
{
    internal class Trade
    {
        public decimal startingCurrency { get; set; }
        public decimal lastPrice { get; set; }
        public decimal closeAt { get; set; }
        public decimal reBuyAt { get; set; }
        public decimal closeAtP { get; set; }
        public decimal reBuyAtP { get; set; }
        public bool fullClose { get; set; }
        public string symbol { get; set; }
        public int chonks { get; set; }
        public decimal tradeParts { get; set; }
    }
}
