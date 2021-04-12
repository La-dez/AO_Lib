using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using static AO_Lib.AO_Devices;

namespace AO_Lib
{
    public class EthernetFilterSSC : EthernetFilter
    {
        public override FilterTypes FilterType => FilterTypes.EthernetFilterSSC;

        public EthernetFilterSSC(string ipAddress, int port) : base(ipAddress, port) { }

        public EthernetFilterSSC(IPEndPoint ipEndPoint) : base(ipEndPoint) { }


    }
}
