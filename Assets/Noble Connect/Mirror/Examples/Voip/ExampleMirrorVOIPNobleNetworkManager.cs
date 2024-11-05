namespace NobleConnect.Examples.Mirror
{
    using NobleConnect.Mirror;
    using UnityEngine;
    using System.Net;
    using System.Text;
    using System.Net.Sockets;
    using System.Threading.Tasks;
    using global::Mirror;
    using System;

    // Example implementation of NobleNetworkManager with support for an extra UDP port for VOIP or whatever
    public class ExampleMirrorVOIPNobleNetworkManager : NobleNetworkManager
    {
        // This is the extra port you want to host something like a voip host on
        public ushort VoipPort = 52341;

        // This is a basic UdpClient used to simulate the voip connection
        // No real voip happens in here, just showing how to get the relay working
        // and using this client to show that data actually flows over the connection
        UdpClient fakeVOIP;

        // The Peer class is the primary interface into the low level Noble Connect stuff we need
        Peer voipPeer;

        // This is address where clients should connect to the voip server.
        // On hosts it is assigned by the relay service and received in OnVOIPHostPrepared
        // On clients, this must be set before calling StartClient() so the voip client knows where to connect
        public IPEndPoint HostVOIPEndPoint;

        // These keep track of when the relay addresses are received since we need both before the host info
        // can be added to matchmaking or otherwise given to clients to use to connect.
        // Really the voip connection and Mirror connection are totally independent and could be handled
        // separately and don't have to have the same lifetime, but I'm trying to keep things simple here.
        bool isMirrorServerReady = false;
        bool isVOIPServerReady = false;

        // This keeps track of our fake voip data so we can see if it was succesfully transferred over the network.
        public string LastFakeVOIPMessage { get; private set; }

        // Force the relays to be used for the voip connection
        // This must be enabled for both client and server to be effective
        public bool forceVOIPOverRelay;

        // This is the basic peer setup. This begins the process of selecting the best region to connect to and other preliminary stuff.
        public void InitPeer()
        {
            var config = NobleConnectSettings.InitConfig();
            config.forceRelayOnly = forceVOIPOverRelay;
            voipPeer = new Peer(config);
        }

        // This is SUPER IMPORTANT. You MUST call Update() on the peer for it to function and you MUST call base.Update() for the NobleNetworkManager to work.
        public override void Update()
        {
            base.Update();
            voipPeer?.Update();
        }

        // Start our fake voip server at the same time we start the normal Mirror server
        public override void OnStartServer()
        {
            base.OnStartServer();
            isMirrorServerReady = false;
            isVOIPServerReady = false;
            InitPeer();
            StartFakeVOIPServer();
        }

        // Start a UDP server and listen for messages from clients on the VoipPort
        // This is where you would start a real VOIP server instead, and make sure it's hosting on the VoipPort
        async void StartFakeVOIPServer()
        {
            IPAddress hostAdress = IPAddress.Any;
            if (Socket.OSSupportsIPv6) hostAdress = IPAddress.IPv6Any;
            fakeVOIP = new UdpClient(new IPEndPoint(hostAdress, VoipPort));
            voipPeer.InitializeHosting((IPEndPoint)fakeVOIP.Client.LocalEndPoint, OnVOIPHostPrepared);
            try
            {
                while (NetworkServer.active && fakeVOIP != null)
                {
                    UdpReceiveResult result = await fakeVOIP.ReceiveAsync();
                    string s = Encoding.ASCII.GetString(result.Buffer, 0, result.Buffer.Length);
                    Debug.Log("Received fake voip message: " + s);
                    LastFakeVOIPMessage = s;
                    byte[] bytesToSend = Encoding.ASCII.GetBytes("Hello!");
                    await fakeVOIP.SendAsync(bytesToSend, bytesToSend.Length, result.RemoteEndPoint);
                }
            }
            catch (ObjectDisposedException)
            {
                // This is the normal path when the UdpClient is closed
            }
        }

        // Start a UDP client and send a message to the host's voip server
        // This is where you would connect your real VOIP client to the provided voipHostAddress
        bool hasReceived = false;
        async void StartFakeVOIPClient(IPEndPoint voipHostAddress)
        {
            fakeVOIP = new UdpClient();
            fakeVOIP.Connect(voipHostAddress);
            WaitForVOIPResponse();
            byte[] bytesToSend = Encoding.ASCII.GetBytes("Hello World");

            try
            {
                while (!hasReceived && fakeVOIP != null)
                {
                    await fakeVOIP.SendAsync(bytesToSend, bytesToSend.Length);
                    await Task.Delay(100);
                }
            }
            catch (ObjectDisposedException)
            {
                // This is the normal path when the UdpClient is closed
            }
        }

        // Wait for a response on the client so we can be sure data is flowing both ways
        async void WaitForVOIPResponse()
        {
            try
            {
                UdpReceiveResult response = await fakeVOIP.ReceiveAsync();
                string s = Encoding.ASCII.GetString(response.Buffer, 0, response.Buffer.Length);
                hasReceived = true;
                Debug.Log("Received fake voip response: " + s);
                LastFakeVOIPMessage = s;
            }
            catch (ObjectDisposedException)
            {
                // This is the normal path when the UdpClient is closed
            }
        }

        // Called when stopping host or client to also stop VOIP
        // This would be a good place to stop your real VOIP host/client
        public void StopVOIP()
        {
            if (fakeVOIP != null)
            {
                fakeVOIP.Close();
                fakeVOIP = null;
            }
            HostVOIPEndPoint = null;
            isVOIPServerReady = false;
            if (voipPeer != null) voipPeer.Dispose();
            voipPeer = null;
        }

        // This is where the client starts finding the route to the VOIP host
        // But we can't make the connection yet, we must wait for OnVOIPClientPrepared
        public override void StartClient()
        {
            base.StartClient();
            InitPeer();
            voipPeer.InitializeClient(HostVOIPEndPoint, OnVOIPClientPrepared);
        }

        // Ok, now connect the voip client to one of the provided endpoints
        void OnVOIPClientPrepared(IPEndPoint routeEndPointIPv4, IPEndPoint routeEndPointIPv6)
        {
            StartFakeVOIPClient(routeEndPointIPv4);
        }

        // Called when the VOIP relay address has been received on the host
        // Once this and OnServerPrepared have both been called we will have all
        // the info we need to be able to connect a client
        void OnVOIPHostPrepared(string ip, ushort port)
        {
            // Get your VOIP host endpoint here
            HostVOIPEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
            isMirrorServerReady = true;
            if (isVOIPServerReady)
            {
                OnReadyForMatchmaking(HostEndPoint, HostVOIPEndPoint);
            }
        }

        // OnServerPrepared is called when the host is listening and has received 
        // their HostEndPoint from the NobleConnect service.
        public override void OnServerPrepared(string hostAddress, ushort hostPort)
        {
            // Get your HostEndPoint here. 
            Debug.Log("Hosting at: " + hostAddress + ":" + hostPort);
            isVOIPServerReady = true;
            if (isMirrorServerReady)
            {
                OnReadyForMatchmaking(HostEndPoint, HostVOIPEndPoint);
            }
        }

        // Use these endpoints on the client in order to connect to the host.
        // Typically you would use a matchmaking system to pass the endpoints to the client.
        // Look at the Match Up Example for one way to do it. Match Up comes free with any paid plan. 
        virtual public void OnReadyForMatchmaking(IPEndPoint mirrorHostAddress, IPEndPoint voipHostAddress) 
        {
            // At this point both Mirror and the VOIP server are ready to receive connections.
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            StopVOIP();
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            StopVOIP();
        }

        public override void OnApplicationQuit()
        {
            base.OnApplicationQuit();
            StopVOIP();
        }
    }
}
