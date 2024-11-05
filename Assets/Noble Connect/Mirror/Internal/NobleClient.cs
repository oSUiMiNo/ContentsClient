using NobleConnect.Ice;
using System;
using System.Net;
using System.Text;
using UnityEngine;
using Mirror;
using System.Net.Sockets;

namespace NobleConnect.Mirror
{
    /// <summary>Adds relay, punchthrough, and port-forwarding support to the Mirror NetworkClient</summary>
    /// <remarks>
    /// Use the Connect method to connect to a host.
    /// </remarks>
    public class NobleClient
    {
        #region Public Properties

        private ConnectionType _latestConnectionType = ConnectionType.NONE;

        /// <summary>You can check this in OnClientConnect(), it will either be Direct, Punchthrough, or Relay.</summary>
        public ConnectionType latestConnectionType {
            get {
                if (baseClient != null) return baseClient.latestConnectionType;
                else return _latestConnectionType;
            }
            set {
                _latestConnectionType = value;
            }
        }

        /// <summary>A convenient way to check if a connection is in progress</summary>
        public bool isConnecting = false;

        /// <summary>Store force relay so that we can pass it on to the iceController</summary>
        public bool ForceRelayOnly;

        #endregion

        #region Internal Properties

        /// <summary>A method to call if something goes wrong like reaching ccu or bandwidth limit</summary>
        Action<Exception> onFatalError = null;

        /// <summary>We store the end point of the local bridge that we connect to because Mirror makes it hard to get the ip and port that a client has connected to for some reason</summary>
        IPEndPoint hostBridgeEndPoint;

        Peer baseClient;
        IceConfig nobleConfig = new IceConfig();

        #endregion Internal Properties

        #region Public Interface

        /// <summary>Initialize the client using NobleConnectSettings. The region used is determined by the Relay Server Address in the NobleConnectSettings.</summary>
        /// <remarks>
        /// The default address is connect.noblewhale.com, which will automatically select the closest 
        /// server based on geographic region.
        /// 
        /// If you would like to connect to a specific region you can use one of the following urls:
        /// <pre>
        ///     us-east.connect.noblewhale.com - Eastern United States
        ///     us-west.connect.noblewhale.com - Western United States
        ///     eu.connect.noblewhale.com - Europe
        ///     ap.connect.noblewhale.com - Asia-Pacific
        ///     sa.connect.noblewhale.com - South Africa
        ///     hk.connect.noblewhale.com - Hong Kong
        /// </pre>
        /// 
        /// Note that region selection will ensure each player connects to the closest relay server, but it does not 
        /// prevent players from connecting across regions. If you want to prevent joining across regions you will 
        /// need to implement that separately (by filtering out unwanted regions during matchmaking for example).
        /// </remarks>
        /// <param name="topo">The HostTopology to use for the NetworkClient. Must be the same on host and client.</param>
        /// <param name="onFatalError">A method to call if something goes horribly wrong.</param>
        /// <param name="allocationResendTimeout">Initial timeout before resending refresh messages. This is doubled for each failed resend.</param>
        /// <param name="maxAllocationResends">Max number of times to try and resend refresh messages before giving up and shutting down the relay connection. If refresh messages fail for 30 seconds the relay connection will be closed remotely regardless of these settings.</param>
        public NobleClient(GeographicRegion region = GeographicRegion.AUTO, Action<Exception> onFatalError = null, int relayLifetime = 60, int relayRefreshTime = 30, float allocationResendTimeout = .1f, int maxAllocationResends = 8, float requestTimeout = .2f)
        {
            var settings = (NobleConnectSettings)Resources.Load("NobleConnectSettings", typeof(NobleConnectSettings));

            this.onFatalError = onFatalError;
            nobleConfig = NobleConnectSettings.InitConfig();
            nobleConfig.protocolType = MirrorHelper.TransportProtocol;
            nobleConfig.iceServerAddress = RegionURL.URLFromRegion(region);
            nobleConfig.RelayRefreshMaxAttempts = maxAllocationResends;
            nobleConfig.RelayRequestTimeout = allocationResendTimeout;
            nobleConfig.RelayLifetime = relayLifetime;
            nobleConfig.RelayRefreshTime = relayRefreshTime;
            nobleConfig.RequestTimeout = requestTimeout;
            nobleConfig.enableIPv6 = false;

            Init();
        }

        public NobleClient() : base()
        {

        }

        /// <summary>
        /// Initialize the client using NobleConnectSettings but connect to specific relay server address.
        /// This method is useful for selecting the region to connect to at run time when starting the client.
        /// </summary>
        /// <remarks>\copydetails NobleClient::NobleClient(HostTopology,Action)</remarks>
        /// <param name="relayServerAddress">The url or ip of the relay server to connect to</param>
        /// <param name="topo">The HostTopology to use for the NetworkClient. Must be the same on host and client.</param>
        /// <param name="onFatalError">A method to call if something goes horribly wrong.</param>
        /// <param name="allocationResendTimeout">Initial timeout before resending refresh messages. This is doubled for each failed resend.</param>
        /// <param name="maxAllocationResends">Max number of times to try and resend refresh messages before giving up and shutting down the relay connection. If refresh messages fail for 30 seconds the relay connection will be closed remotely regardless of these settings.</param>
        public NobleClient(string relayServerAddress, Action<Exception> onFatalError = null, int relayLifetime = 60, int relayRefreshTime = 30, float allocationResendTimeout = .1f, int maxAllocationResends = 8, float requestTimeout = .2f)
        {
            this.onFatalError = onFatalError;

            nobleConfig = NobleConnectSettings.InitConfig();
            nobleConfig.protocolType = MirrorHelper.TransportProtocol;
            nobleConfig.iceServerAddress = relayServerAddress;
            nobleConfig.RelayRefreshMaxAttempts = maxAllocationResends;
            nobleConfig.RelayRequestTimeout = allocationResendTimeout;
            nobleConfig.RelayLifetime = relayLifetime;
            nobleConfig.RelayRefreshTime = relayRefreshTime;
            nobleConfig.RequestTimeout = requestTimeout;

            Init();
        }

        public GeographicRegion GetConnectedRegion()
        {
            return baseClient.GetConnectedRegion();
        }

        /// <summary>Prepare to connect but don't actually connect yet</summary>
        /// <remarks>
        /// This is used when initializing a client early before connecting. Getting this
        /// out of the way earlier can make the actual connection seem quicker.
        /// </remarks>
        public void PrepareToConnect()
        {
            nobleConfig.forceRelayOnly = ForceRelayOnly;
            baseClient.PrepareToConnect();
        }

        /// <summary>If you are using the NetworkClient directly you must call this method every frame.</summary>
        /// <remarks>
        /// The NobleNetworkManager and NobleNetworkLobbyManager handle this for you but you if you are
        /// using the NobleClient directly you must make sure to call this method every frame.
        /// </remarks>
        public void Update()
        {
			if (baseClient != null) baseClient.Update();
        }

        /// <summary>Connect to the provided host ip and port</summary>
        /// <remarks>
        /// Note that the host address used here should be the one provided to the host by 
        /// the relay server, not the actual ip of the host's computer. You can get this 
        /// address on the host from Server.HostEndPoint.
        /// </remarks>
        /// <param name="hostIP">The IP of the server's HostEndPoint</param>
        /// <param name="hostPort">The port of the server's HostEndPoint</param>
        /// <param name="topo">The HostTopology to use for the NetworkServer.</param>
        public void Connect(string hostIP, ushort hostPort, bool isLANOnly = false)
        {
            Connect(new IPEndPoint(IPAddress.Parse(hostIP), hostPort), isLANOnly);
        }

        /// <summary>Connect to the provided HostEndPoint</summary>
        /// <remarks>
        /// Note that the host address used here should be the one provided to the host by 
        /// the relay server, not the actual ip of the host's computer. You can get this 
        /// address on the host from Server.HostEndPoint.
        /// </remarks>
        /// <param name="hostEndPoint">The HostEndPoint of the server to connect to</param>
        /// <param name="hostPort">The port of the server's HostEndPoint</param>
        /// <param name="topo">The HostTopology to use for the NetworkServer.</param>
        public void Connect(IPEndPoint hostEndPoint, bool isLANOnly = false)
        {
            if (isConnecting || isConnected) return;
            isConnecting = true;

            if (isLANOnly)
            {
                MirrorHelper.TransportPort = (ushort)hostEndPoint.Port;
                NetworkClient.Connect(hostEndPoint.Address.ToString());
            }
            else
            {
                if (baseClient == null)
                {
                    Init();
                }
                baseClient.InitializeClient(hostEndPoint, OnReadyToConnect);
            }
        }

        /// <summary>Shut down the client and clean everything up.</summary>
        /// <remarks>
        /// You can call this method if you are totally done with a client and don't plan
        /// on using it to connect again.
        /// </remarks>
        public void Shutdown()
        {
            if (baseClient != null)
            {
                baseClient.CleanUpEverything();
                baseClient.Dispose();
                baseClient = null;
            }


            NetworkClient.Shutdown();
        }

        /// <summary>Clean up and free resources. Called automatically when garbage collected.</summary>
        /// <remarks>
        /// You shouldn't need to call this directly. It will be called automatically when an unused
        /// NobleClient is garbage collected or when shutting down the application.
        /// </remarks>
        /// <param name="disposing"></param>
        public void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (NetworkClient.active)
                {
                    NetworkClient.Shutdown();
                }
                if (baseClient != null) baseClient.Dispose();
            }
            isConnecting = false;
        }
        public void Dispose()
        {
            Dispose(true);
        }

        #endregion Public Interface

        #region Internal Methods

        /// <summary>Initialize the NetworkClient and NobleConnect client</summary>
        private void Init()
        {
            var platform = Application.platform;
            nobleConfig.useSimpleAddressGathering = (platform == RuntimePlatform.IPhonePlayer || platform == RuntimePlatform.Android) && !Application.isEditor;
            nobleConfig.onFatalError = onFatalError;
            nobleConfig.forceRelayOnly = ForceRelayOnly;

            baseClient = new Peer(nobleConfig);
            NetworkClient.OnConnectedEvent = OnClientConnect;
            NetworkClient.OnDisconnectedEvent = OnClientDisconnect;
        }

        #endregion Internal Methods

        #region Handlers

        /// <summary>Called when Noble Connect has selected a candidate pair to use to connect to the host.</summary>
        /// <param name="bridgeEndPoint">The EndPoint to connect to</param>
        private void OnReadyToConnect(IPEndPoint bridgeEndPoint, IPEndPoint bridgeEndPointIPv6)
        {
            if (Socket.OSSupportsIPv6 && bridgeEndPointIPv6 != null)
            {
                hostBridgeEndPoint = bridgeEndPointIPv6;
                MirrorHelper.TransportPort = (ushort)bridgeEndPointIPv6.Port;
                NetworkClient.Connect(bridgeEndPointIPv6.Address.ToString());
            }
            else
            {
                hostBridgeEndPoint = bridgeEndPoint;
                MirrorHelper.TransportPort = (ushort)bridgeEndPoint.Port;
                NetworkClient.Connect(bridgeEndPoint.Address.ToString());
            }
            if (MirrorHelper.TransportProtocol == ProtocolType.Udp)
            {
                baseClient.SetLocalEndPoint(MirrorHelper.LocalTransportEndPoint);
            }
        }

        /// <summary>Called on the client upon succesfully connecting to a host</summary>
        /// <remarks>
        /// We clean some ice stuff up here.
        /// </remarks>
        /// <param name="message"></param>
        virtual public void OnClientConnect()
        {
            // This happens when connecting in LAN only mode, which is always direct
            if (baseClient == null) latestConnectionType = ConnectionType.DIRECT;

            isConnecting = false;
            if (baseClient != null)
            {
                baseClient.FinalizeConnection(hostBridgeEndPoint);
            }
        }

        /// <summary>Called on the client upon disconnecting from a host</summary>
        /// <remarks>
        /// Some memory and ports are freed here.
        /// </remarks>
        /// <param name="message"></param>
        virtual public void OnClientDisconnect()
        {
            if (baseClient != null) baseClient.EndSession(hostBridgeEndPoint);
        }

        #endregion Handlers

        #region Mirror NetworkClient public interface

#if !DOXYGEN_SHOULD_SKIP_THIS
        /// The rest of this is just a wrapper for Mirror's NetworkClient

        /// <summary>
        /// The NetworkConnection object this client is using.
        /// </summary>
        public NetworkConnection connection => NetworkClient.connection;

        /// <summary>
        /// active is true while a client is connecting/connected
        /// (= while the network is active)
        /// </summary>
        public bool active => NetworkClient.active;

        /// <summary>
        /// This gives the current connection status of the client.
        /// </summary>
        public bool isConnected => NetworkClient.isConnected;

        /// <summary>True if client is running in host mode.</summary>
        public bool activeHost => NetworkClient.activeHost;

        /// <summary>
        /// Connect client to a NetworkServer instance.
        /// </summary>
        /// <param name="address"></param>
        public void Connect(string address)
        {
            NetworkClient.Connect(address);
        }

        /// <summary>
        /// Connect client to a NetworkServer instance.
        /// </summary>
        /// <param name="uri">Address of the server to connect to</param>
        public void Connect(Uri uri)
        {
            NetworkClient.Connect(uri);
        }

        public void ConnectHost()
        {
            NetworkClient.ConnectHost();
        }

        /// <summary>
        /// disconnect host mode. this is needed to call DisconnectMessage for
        /// the host client too.
        /// </summary>
        public void DisconnectLocalServer()
        {
            NetworkClient.Disconnect();
        }

        /// <summary>
        /// Disconnect from server.
        /// <para>The disconnect message will be invoked.</para>
        /// </summary>
        public void Disconnect()
        {
            NetworkClient.Disconnect();
        }

        /// <summary>
        /// This sends a network message with a message Id to the server. This message is sent on channel zero, which by default is the reliable channel.
        /// <para>The message must be an instance of a class derived from MessageBase.</para>
        /// <para>The message id passed to Send() is used to identify the handler function to invoke on the server when the message is received.</para>
        /// </summary>
        /// <typeparam name="T">The message type to unregister.</typeparam>
        /// <param name="message"></param>
        /// <param name="channelId"></param>
        public void Send<T>(T message, int channelId = Channels.Reliable) where T : struct, NetworkMessage
        {
            NetworkClient.Send<T>(message, channelId);
        }

        /// <summary>
        /// Register a handler for a particular message type.
        /// <para>There are several system message types which you can add handlers for. You can also add your own message types.</para>
        /// </summary>
        /// <typeparam name="T">Message type</typeparam>
        /// <param name="handler">Function handler which will be invoked when this message type is received.</param>
        /// <param name="requireAuthentication">True if the message requires an authenticated connection</param>
        public void RegisterHandler<T>(Action<T> handler, bool requireAuthentication = true) where T : struct, NetworkMessage
        {
            NetworkClient.RegisterHandler<T>(handler, requireAuthentication);
        }

        /// <summary>
        /// Replaces a handler for a particular message type.
        /// <para>See also <see cref="RegisterHandler{T}(Action{NetworkConnection, T}, bool)">RegisterHandler(T)(Action(NetworkConnection, T), bool)</see></para>
        /// </summary>
        /// <typeparam name="T">Message type</typeparam>
        /// <param name="handler">Function handler which will be invoked when this message type is received.</param>
        /// <param name="requireAuthentication">True if the message requires an authenticated connection</param>
        public void ReplaceHandler<T>(Action<T> handler, bool requireAuthentication = true) where T : struct, NetworkMessage
        {
            NetworkClient.ReplaceHandler<T>(handler, requireAuthentication);
        }

        /// <summary>
        /// Unregisters a network message handler.
        /// </summary>
        /// <typeparam name="T">The message type to unregister.</typeparam>
        public bool UnregisterHandler<T>() where T : struct, NetworkMessage
        {
            return NetworkClient.UnregisterHandler<T>();
        }
    }

    #endif

    #endregion
}
