using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleTrader.Models
{
    public class Trade
    {
        public string apiKey { get; set; }
        public string apiSecret { get; set; }
        public string apiPassphrase { get; set; }
        public int sandbox { get; set; }
        public int manual { get; set; }
        public int balance { get; set; }
        public int divides { get; set; }
        public Calclist[] calcList { get; set; }
        public int interval { get; set; }
        public int neat { get; set; }
        public string pair { get; set; }
    }

    public class Calclist
    {
        public decimal up { get; set; }
        public decimal down { get; set; }
    }

}
