using Mirror;
using System.Net;
using System.Net.Sockets;

public interface ITransportHelper
{
    ProtocolType Protocol { get; }
    ushort Port { get; set; }
    IPEndPoint LocalEndPoint { get; }
    IPEndPoint GetClientEndPoint(NetworkConnection conn);
}

public abstract class TransportHelper<T> : ITransportHelper where T : Transport
{
    protected T transport;

    public TransportHelper(T transport) => this.transport = transport;

    public abstract ProtocolType Protocol { get; }
    public abstract ushort Port { get; set; }
    public abstract IPEndPoint LocalEndPoint { get; }
    public abstract IPEndPoint GetClientEndPoint(NetworkConnection conn);
}