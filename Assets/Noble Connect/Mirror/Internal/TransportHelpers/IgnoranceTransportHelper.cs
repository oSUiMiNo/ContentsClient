#if IGNORANCE
using Mirror;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

public class IgnoranceTransportHelper : TransportHelper<IgnoranceTransport.Ignorance>
{
    public IgnoranceTransportHelper() : base((IgnoranceTransport.Ignorance)Transport.active) { }

    public override ProtocolType Protocol => ProtocolType.Udp;
    public override ushort Port { get => (ushort)transport.port; set => transport.port = value; }

    public override IPEndPoint LocalEndPoint => transport.GetLocalEndPoint();

    public override IPEndPoint GetClientEndPoint(NetworkConnection conn)
    {
        string endpoint = transport.ServerGetClientAddress(conn.connectionId);
        int portStartIndex = endpoint.LastIndexOf(":");
        string address = endpoint.Substring(0, portStartIndex);
        ushort port = ushort.Parse(endpoint.Substring(portStartIndex + 1));
        return new IPEndPoint(IPAddress.Parse(address), port);
    }
}
#endif