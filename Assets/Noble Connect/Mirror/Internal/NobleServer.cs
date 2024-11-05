using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using NobleConnect.Ice;
using UnityEngine;
using Mirror;

namespace NobleConnect.Mirror
{
    /// <summary>Adds relay, punchthrough, and port-forwarding support to the Mirror NetworkServer</summary>
    /// <remarks>
    /// Use the Listen method to start listening for incoming connections.
    /// </remarks>
    public class NobleServer
    {

        #region Public Properties

        static IPEndPoint LocalHostEndPoint = null;
        /// <summary>This is the address that clients should connect to. It is assigned by the relay server.</summary>
        /// <remarks>
        /// Note that this is not the host's actual IP address, but one assigned to the host by the relay server.
        /// When clients connect to this address, Noble Connect will find the best possible connection and use it.
        /// This means that the client may actually end up connecting to an address on the local network, or an address
        /// on the router, or an address on the relay. But you don't need to worry about any of that, it is all
        /// handled for you internally.
        /// </remarks>
        public static IPEndPoint HostEndPoint {
            get {
                if (baseServer != null) 
                {
                    return baseServer.RelayEndPoint;
                }
                else
                {
                    return LocalHostEndPoint;
                }
            }
            set {
                LocalHostEndPoint = value;
            }
        }

        /// <summary>Initial timeout before resending refresh messages. This is doubled for each failed resend.</summary>
        public static float allocationResendTimeout = .1f;

        /// <summary>Request timeout.</summary>
        /// <remarks>
        /// This effects how long to wait before considering a request to have failed.
        /// Requests are used during the punchthrough process and for setting up and maintaining relays.
        /// If you are allowing cross-region play or expect high latency you can increase this so that requests won't time out.
        /// The drawback is that waiting longer for timeouts causes it take take longer to detect actual failed requests so the
        /// connection process may take longer.
        /// </remarks>
        public static float requestTimeout = .2f;

        /// <summary>Max number of times to try and resend refresh messages before giving up and shutting down the relay connection.</summary>
        /// <remarks>
        /// If refresh messages fail for 30 seconds the relay connection will be closed remotely regardless of these settings.
        /// </remarks>
        public static int maxAllocationResends = 8;

        /// <summary>How long a relay will stay alive without being refreshed (in seconds)</summary>
        /// <remarks>
        /// Setting this value higher means relays will stay alive longer even if the host temporarily loses connection or otherwise fails to send the refresh request in time.
        /// This can be helpful to maintain connection on an undependable network or when heavy application load (such as loading large levels synchronously) temporarily prevents requests from being processed.
        /// The drawback is that CCU is used for as long as the relay stays alive, so players that crash or otherwise don't clean up properly can cause lingering CCU usage for up to relayLifetime seconds.
        /// </remarks>
        public static int relayLifetime = 60;
        
        /// <summary>How often to refresh the relay to keep it alive, in seconds</summary>
        public static int relayRefreshTime = 30;

        #endregion Public Properties

        #region Internal Properties

        const string TRANSPORT_WARNING_MESSAGE = "You must use a transport that supports UDP in order to use Mirror with NobleConnect.\n" +
                                                "I recommend the default KcpTransport.";

        static Peer baseServer;

        /// <summary>A method to call if something goes wrong like reaching ccu or bandwidth limit</summary>
        static Action<Exception> onFatalError = null;
        /// <summary>Keeps track of which end point each NetworkConnection belongs to so that when they disconnect we know which Bridge to destroy</summary>
        static Dictionary<NetworkConnection, IPEndPoint> endPointByConnection = new Dictionary<NetworkConnection, IPEndPoint>();

        static IceConfig nobleConfig = new IceConfig();

        #endregion Internal Properties

        #region Public Interface

        /// <summary>Initialize the server using NobleConnectSettings. The region used is determined by the Relay Server Address in the NobleConnectSettings.</summary>
        /// <remarks>\copydetails NobleClient::NobleClient(HostTopology,Action)</remarks>
        /// <param name="topo">The HostTopology to use for the NetworkClient. Must be the same on host and client.</param>
        /// <param name="onFatalError">A method to call if something goes horribly wrong.</param>
        public static void InitializeHosting(IPEndPoint localEndPoint, GeographicRegion region = GeographicRegion.AUTO, Action<string, ushort> onPrepared = null, Action<Exception> onFatalError = null, bool forceRelayOnly = false)
        {
            _Init(localEndPoint, RegionURL.URLFromRegion(region), onPrepared, onFatalError, forceRelayOnly);
        }

        /// <summary>
        /// Initialize the client using NobleConnectSettings but connect to specific relay server address.
        /// This method is useful for selecting the region to connect to at run time when starting the client.
        /// </summary>
        /// <remarks>\copydetails NobleClient::NobleClient(HostTopology,Action)</remarks>
        /// <param name="relayServerAddress">The url or ip of the relay server to connect to</param>
        /// <param name="topo">The HostTopology to use for the NetworkClient. Must be the same on host and client.</param>
        /// <param name="onPrepared">A method to call when the host has received their HostEndPoint from the relay server.</param>
        /// <param name="onFatalError">A method to call if something goes horribly wrong.</param>
        public static void InitializeHosting(IPEndPoint localEndPoint, string relayServerAddress, Action<string, ushort> onPrepared = null, Action<Exception> onFatalError = null, bool forceRelayOnly = false)
        {
            _Init(localEndPoint, relayServerAddress, onPrepared, onFatalError, forceRelayOnly);
        }


        /// <summary>Initialize the NetworkServer and Ice.Controller</summary>
        static void _Init(IPEndPoint localEndPoint, string relayServerAddress, Action<string, ushort> onPrepared = null, Action<Exception> onFatalError = null, bool forceRelayOnly = false)
        {
            var platform = Application.platform;

            NobleServer.onFatalError = onFatalError;

            nobleConfig = NobleConnectSettings.InitConfig();

            // Well isn't this just the smelliest code smell
            nobleConfig.protocolType = MirrorHelper.TransportProtocol;
            nobleConfig.iceServerAddress = relayServerAddress;
            nobleConfig.onFatalError = OnFatalError;
            nobleConfig.forceRelayOnly = forceRelayOnly;
            nobleConfig.RelayLifetime = relayLifetime;
            nobleConfig.RelayRefreshTime = relayRefreshTime;
            nobleConfig.RelayRefreshMaxAttempts = maxAllocationResends;
            nobleConfig.RelayRequestTimeout = allocationResendTimeout;
            nobleConfig.RequestTimeout = requestTimeout;

            baseServer = new Peer(nobleConfig);

            NetworkServer.OnConnectedEvent = OnServerConnect;
            NetworkServer.OnDisconnectedEvent = OnServerDisconnect;

            if (baseServer == null)
            {
                Logger.Log("NobleServer.Init() must be called before InitializeHosting() to specify region and error handler.", Logger.Level.Fatal);
            }
            baseServer.InitializeHosting(localEndPoint, onPrepared);
        }

        static public GeographicRegion GetConnectedRegion()
        {
            return baseServer.GetConnectedRegion();
        }

        /// <summary>If you are using the NetworkServer directly you must call this method every frame.</summary>
        /// <remarks>
        /// The NobleNetworkManager and NobleNetworkLobbyManager handle this for you but you if you are
        /// using the NobleServer directly you must make sure to call this method every frame.
        /// </remarks>
        static public void Update()
        {
            if (baseServer != null) baseServer.Update();
        }

        /// <summary>Start listening for incoming connections</summary>
        /// <param name="maxPlayers">The maximum number of players</param>
        /// <param name="port">The port to listen on. Defaults to 0 which will use a random port</param>
        /// <param name="onPrepared">A method to call when the host has received their HostEndPoint from the relay server.</param>
        //static public void Listen(int maxConnections, IPEndPoint localEndPoint, GeographicRegion region = GeographicRegion.AUTO, Action<string, ushort> onPrepared = null, Action<Exception> onFatalError = null, bool forceRelayOnly = false)
        //{
        //    // Store or generate the server port
        //    if (port == 0)
        //    {
        //        // Use a randomly generated endpoint
        //        using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
        //        {
        //            socket.Bind(new IPEndPoint(IPAddress.Any, 0));
        //            port = (ushort)((IPEndPoint)socket.LocalEndPoint).Port;
        //        }
        //    }

        //    InitializeHosting(localEndPoint, region, onPrepared, onFatalError, forceRelayOnly);

        //    MirrorHelper.SetTransportPort((ushort)port);
        //    // Server Go!
        //    NetworkServer.Listen(maxConnections);
        //}

        static public void UnregisterHandler<T>() where T : struct, NetworkMessage
        {
            NetworkServer.UnregisterHandler<T>();
        }

        static public void ClearHandlers()
        {
            NetworkServer.ClearHandlers();
        }

        /// <summary>Clean up and free resources. Called automatically when garbage collected.</summary>
        /// <remarks>
        /// You shouldn't need to call this directly. It will be called automatically when an unused
        /// NobleServer is garbage collected or when shutting down the application.
        /// </remarks>
        /// <param name="disposing"></param>
        public static void Dispose()
        {
            if (baseServer != null)
            {
                baseServer.Dispose();
                baseServer = null;
            }
        }

#endregion Public Interface

        #region Handlers

        static public void OnServerConnect(NetworkConnection conn)
        {
            if (baseServer == null) return;
            
            if (conn.GetType() == typeof(NetworkConnectionToClient))
            {
                IPEndPoint clientEndPoint = MirrorHelper.GetClientEndPoint(conn);
                endPointByConnection[conn] = clientEndPoint;
                baseServer.FinalizeConnection(clientEndPoint);
            }
        }

        /// <summary>Called on the server when a client disconnects</summary>
        /// <remarks>
        /// Some memory and ports are freed here.
        /// </remarks>
        /// <param name="message"></param>
        static public void OnServerDisconnect(NetworkConnection conn)
        {
            if (endPointByConnection.ContainsKey(conn))
            {
                IPEndPoint endPoint = endPointByConnection[conn];
                if (baseServer != null) baseServer.EndSession(endPoint);
                endPointByConnection.Remove(conn);
            }
            //conn.Dispose();
        }

        /// <summary>Called when a fatal error occurs.</summary>
        /// <remarks>
        /// This usually means that the ccu or bandwidth limit has been exceeded. It will also
        /// happen if connection is lost to the relay server for some reason.
        /// </remarks>
        /// <param name="errorString">A string with more info about the error</param>
        static private void OnFatalError(Exception errorString)
        {
            if (onFatalError != null) onFatalError(errorString);
            NetworkServer.Shutdown();
        }

        #endregion Handlers

        #region Static NetworkServer Wrapper
        static public NetworkConnection localConnection { get { return NetworkServer.localConnection; } }
        static public Dictionary<int, NetworkConnectionToClient> connections { get { return NetworkServer.connections; } }
        public static bool dontListen { get { return NetworkServer.dontListen; } set { NetworkServer.dontListen = value; } }
        public static bool active { get { return NetworkServer.active; } }
        public static bool activeHost { get { return NetworkServer.activeHost; } }
        public static void Shutdown() { NetworkServer.Shutdown(); }
        public static void SendToAll<T>(T msg, int channelId = Channels.Reliable, bool sendToReadyOnly = false) where T : struct, NetworkMessage
        {
            NetworkServer.SendToAll<T>(msg, channelId, sendToReadyOnly);
        }
        public static void SendToReadyObservers<T>(NetworkIdentity identity, T msg, bool includeSelf = true, int channelId = Channels.Reliable) where T : struct, NetworkMessage
        {
            NetworkServer.SendToReadyObservers<T>(identity, msg, includeSelf, channelId);
        }
        public static void SendToReadyObservers<T>(NetworkIdentity identity, T msg, int channelId = Channels.Reliable) where T : struct, NetworkMessage
        {
            SendToReadyObservers(identity, msg, true, channelId);
        }
        static public void DisconnectAll() { NetworkServer.DisconnectAll(); }
        static public void SendToClientOfPlayer<T>(NetworkIdentity player, T msg) where T : struct, NetworkMessage
        {
            player.connectionToClient.Send<T>(msg);
        }
        static public bool ReplacePlayerForConnection(NetworkConnectionToClient conn, GameObject player, uint assetId, bool keepAuthority = false)
        {
            return NetworkServer.ReplacePlayerForConnection(conn, player, assetId, keepAuthority);
        }
        public static bool ReplacePlayerForConnection(NetworkConnectionToClient conn, GameObject player, bool keepAuthority = false)
        {
            return NetworkServer.ReplacePlayerForConnection(conn, player, keepAuthority);
        }
        static public bool AddPlayerForConnection(NetworkConnectionToClient conn, GameObject player, uint assetId)
        {
            return NetworkServer.AddPlayerForConnection(conn, player, assetId);
        }
        static public bool AddPlayerForConnection(NetworkConnectionToClient conn, GameObject player)
        {
            return NetworkServer.AddPlayerForConnection(conn, player);
        }
        static public void SetClientReady(NetworkConnectionToClient conn) { NetworkServer.SetClientReady(conn); }
        static public void SetAllClientsNotReady() { NetworkServer.SetAllClientsNotReady(); }
        static public void SetClientNotReady(NetworkConnectionToClient conn) { NetworkServer.SetClientNotReady(conn); }
        static public void DestroyPlayerForConnection(NetworkConnectionToClient conn) { NetworkServer.DestroyPlayerForConnection(conn); }
        static public void Spawn(GameObject obj) { NetworkServer.Spawn(obj); }
        static public void Spawn(GameObject obj, GameObject player) { NetworkServer.Spawn(obj, player); }
        static public void Spawn(GameObject obj, NetworkConnection conn) { NetworkServer.Spawn(obj, conn); }
        static public void Spawn(GameObject obj, uint assetId, NetworkConnection conn) { NetworkServer.Spawn(obj, assetId, conn); }
        static public void Spawn(GameObject obj, uint assetId) { NetworkServer.Spawn(obj, assetId); }
        static public void Destroy(GameObject obj) { NetworkServer.Destroy(obj); }
        static public void UnSpawn(GameObject obj) { NetworkServer.UnSpawn(obj); }
        static public NetworkIdentity FindLocalObject(uint netId) { return NetworkServer.spawned[netId]; }
        static public bool SpawnObjects() { return NetworkServer.SpawnObjects(); }

        #endregion
    }
}
