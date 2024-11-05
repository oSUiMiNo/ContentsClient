using Mirror;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using Telepathy;

public class TelepathyTransportHelper : TransportHelper<TelepathyTransport>
{
    public TelepathyTransportHelper() : base((TelepathyTransport)Transport.active) { }

    public override ProtocolType Protocol => ProtocolType.Tcp;
    public override ushort Port { get => transport.Port; set => transport.Port = value; }

    public override IPEndPoint LocalEndPoint { 
        get {
            // Boo reflection but whatever, I'm over it
            if (transport.ServerActive())
            {
                FieldInfo serverField = typeof(TelepathyTransport).GetField("server", BindingFlags.Instance | BindingFlags.NonPublic);
                Server server = (Server)serverField.GetValue((TelepathyTransport)Transport.active);
                // I hate this but the listener is set in a thread and may not be ready (especially in builds) by the time we get here
                // so...just wait, it shouldn't take long anyway and if it does there are bigger problems
                while (server.listener == null) Thread.Sleep(10);
                return (IPEndPoint)server.listener.LocalEndpoint;
            }
            else
            {
                FieldInfo stateField = typeof(TelepathyTransport).GetField("state", BindingFlags.Instance | BindingFlags.NonPublic);
                ConnectionState state = (ConnectionState)stateField.GetValue((TelepathyTransport)Transport.active);
                return (IPEndPoint)state.client.Client.LocalEndPoint;
            }
        }
    }

    public override IPEndPoint GetClientEndPoint(NetworkConnection conn)
    {
        // Some annoying reflection here because telepathy normally only returns the address and not the full endpoint with port here
        // but we need both.
        FieldInfo serverField = typeof(TelepathyTransport).GetField("server", BindingFlags.Instance | BindingFlags.NonPublic);
        Server server = (Server)serverField.GetValue((TelepathyTransport)Transport.active);
        FieldInfo clientsField = typeof(Server).GetField("clients", BindingFlags.Instance | BindingFlags.NonPublic);
        var clients = (ConcurrentDictionary<int, ConnectionState>)clientsField.GetValue(server);

        // find the connection
        if (clients.TryGetValue(conn.connectionId, out ConnectionState connection))
        {
            return (IPEndPoint)connection.client.Client.RemoteEndPoint;
        }
        return null;
    }
}