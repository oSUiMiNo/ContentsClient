using kcp2k;
using Mirror;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

public class KCPTransportHelper : TransportHelper<KcpTransport>
{
    public KCPTransportHelper() : base((KcpTransport)Transport.active) { }

    public override ProtocolType Protocol => ProtocolType.Udp;
    public override ushort Port { get => transport.Port; set => transport.Port = value; }

    public override IPEndPoint LocalEndPoint
    {
        get
        {
            if (transport.ServerActive())
            {
                var serverField = typeof(KcpTransport).GetField("server", BindingFlags.Instance | BindingFlags.NonPublic);
                var server = (KcpServer)serverField.GetValue(transport);
                return (IPEndPoint)server.LocalEndPoint;
            }
            else
            {
                var clientField = typeof(KcpTransport).GetField("client", BindingFlags.Instance | BindingFlags.NonPublic);
                var client = (KcpClient)clientField.GetValue((KcpTransport)transport);
                return (IPEndPoint)client.LocalEndPoint;
            }
        }
    }

    public override IPEndPoint GetClientEndPoint(NetworkConnection conn)
    {
        var kcpServerField = typeof(KcpTransport).GetField("server", BindingFlags.NonPublic | BindingFlags.Instance);
        var kcpServer = (kcp2k.KcpServer)kcpServerField.GetValue(transport);
        if (kcpServer.connections.TryGetValue(conn.connectionId, out KcpServerConnection result))
        {
            return (IPEndPoint)result.remoteEndPoint;
        }

        return null;
    }
}