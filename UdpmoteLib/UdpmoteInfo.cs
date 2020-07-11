using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace UdpmoteLib
{
    public class UdpmoteInfo
    {
        public IPAddress address;
        public string name;
        public int num;
        public DateTime latestBC;
        public DateTime latestData;
        public bool isConnected;

        public UdpmoteInfo(IPAddress address, string name, int num, bool isConnected, DateTime latestBC, DateTime latestData)
        {
            this.address = address;
            this.name = name;
            this.num = num;
            this.isConnected = isConnected;
            this.latestBC = latestBC;
            this.latestData = latestData;
        }
    }
}
