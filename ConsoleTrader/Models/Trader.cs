using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleTrader.Models
{
    public class Trader
    {
        public string symbol;
        public decimal quantity;
        public decimal down;
        public decimal up;
        public decimal price;
        public decimal totalPrice;
        public decimal avgPrice;
        public int divides;
        public decimal balance;
    }
}
