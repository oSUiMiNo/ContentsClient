using System;
using System.Net;
using System.Net.Sockets;
using kcp2k;
using Mirror;
using UnityEngine;

namespace NobleConnect.Mirror
{
    public class MirrorHelper
    {
        const string TRANSPORT_WARNING_MESSAGE = "Unsupported transport. Please contact us if you need support for a transport that is not included here.";

        static ITransportHelper transportHelper;

        public static ProtocolType TransportProtocol => transportHelper.Protocol;

        public static void Init()
        {
            var transport = Transport.active;
            var transportType = transport.GetType();
            if (transportType == typeof(LatencySimulation))
            {
                transport = (transport as LatencySimulation).wrap;
            }
            transportType = transport.GetType();

#if IGNORANCE
            if (transportType.IsSubclassOf(typeof(IgnoranceTransport.Ignorance)) || transportType == typeof(IgnoranceTransport.Ignorance))
            {
                transportHelper = new IgnoranceTransportHelper();
            }
#endif
            if (transportType.IsSubclassOf(typeof(KcpTransport)) || transportType == typeof(KcpTransport))
            {
                transportHelper = new KCPTransportHelper();
            }
            if (transportType.IsSubclassOf(typeof(TelepathyTransport)) || transportType == typeof(TelepathyTransport))
            {
                transportHelper = new TelepathyTransportHelper();
            }

            if (transportHelper == null) throw new Exception(TRANSPORT_WARNING_MESSAGE);
        }

        public static ushort TransportPort { get => transportHelper.Port; set => transportHelper.Port = value; }
        public static IPEndPoint LocalTransportEndPoint => transportHelper.LocalEndPoint;
        public static IPEndPoint GetClientEndPoint(NetworkConnection conn) => transportHelper.GetClientEndPoint(conn);
    }
}