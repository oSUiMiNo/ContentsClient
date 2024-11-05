namespace NobleConnect.Examples
{
    using System;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading.Tasks;
    using NobleConnect;
    using NobleConnect.Stun;
    using NobleConnect.Turn;
    using UnityEngine;
    using UnityEngine.UIElements;
    using Logger = Logger;

    public class LowLevelExample : MonoBehaviour
    {
        public ushort voipPort = 42345;
        Peer peer;
        enum MenuState { START, CLIENT, HOST, CLIENT_HAS_ROUTE }
        MenuState menuState = MenuState.START;
        string hostIP;
        ushort hostPort;
        string hostPortString;
        IPEndPoint clientConnectToIPv4;
        IPEndPoint clientConnecToIPv6;

        TcpListener fakeVOIPServer;
        TcpClient fakeVOIPClient;

        //Stun.Controller stunController;
        //TurnExtension turnExtension;

        void Start()
        {
            Logger.logger = Debug.Log;
            Logger.logLevel = Logger.Level.Developer;
        }

        private void OnDestroy()
        {
            peer?.Dispose();
            peer = null;
            if (fakeVOIPServer != null) fakeVOIPServer.Stop();
            if (fakeVOIPClient != null) fakeVOIPClient.Close();
        }

        void Update()
        {
            peer?.Update();
        }

        void OnGUI()
        {
            switch (menuState)
            {
                case MenuState.START: StartGUI(); break;
                case MenuState.CLIENT: ClientGUI(); break;
                case MenuState.CLIENT_HAS_ROUTE: ClientWithRouteGUI(); break;
                case MenuState.HOST: HostGUI(); break;
            }
        }

        void StartGUI()
        {
            if (GUI.Button(new Rect(10, 10, 100, 60), "Host"))
            {
                //stunController.Connect();
                //turnExtension.SendAllocateRequest(
                //    m => {
                //        var endpoint = m.GetAttribute<AttributeXORMappedAddress>(Turn.AttributeType.XORRelayedAddress);
                //        OnHostPrepared(endpoint.IPAddress.ToString(), endpoint.port);
                //    }, 
                //    m => { 
                //    }
                //);
                FakeVOIPServer();
            }
            if (GUI.Button(new Rect(10, 70, 100, 60), "Client"))
            {
                menuState = MenuState.CLIENT;
            }
        }

        void CreatePeer()
        {
            var iceConfig = NobleConnectSettings.InitConfig();
            iceConfig.protocolType = ProtocolType.Tcp;
            iceConfig.iceServerAddress = "us-east.connect.noblewhale.com";
            iceConfig.enableIPv6 = true;
            peer = new Peer(iceConfig);

            //var config = new Stun.ControllerConfig();
            //config.username = iceConfig.username;
            //config.password = iceConfig.password;
            //config.origin = iceConfig.origin;
            //config.stunServerEndPoint = new IPEndPoint(IPAddress.Parse("159.203.136.135"), 3478);
            //var socketHandler = SocketHandler.CreateTCPSocket(AddressFamily.InterNetwork, 4096, 1);
            //stunController = new Stun.Controller(config, socketHandler);
            //turnExtension = new TurnExtension(config.stunServerEndPoint);
            //stunController.AddExtension(turnExtension);
        }

        void FakeVOIPServer()
        {
            fakeVOIPServer = new TcpListener(IPAddress.IPv6Any, voipPort);
            fakeVOIPServer.Start();
            CreatePeer();
            peer.InitializeHosting((IPEndPoint)fakeVOIPServer.Server.LocalEndPoint, OnHostPrepared);
            ListenForIncomingConnections();
        }

        async void ListenForIncomingConnections()
        {
            try
            {
                while (fakeVOIPServer != null)
                {
                    TcpClient client = await fakeVOIPServer.AcceptTcpClientAsync();
                    ReceiveFromClient(client);
                }
            }
            catch
            {
                // Server has gone away
            }
        }

        async void ReceiveFromClient(TcpClient client)
        {
            byte[] someBytes = new byte[1024];
            try
            {
                while (client.Connected)
                {
                    int numRead = await client.GetStream().ReadAsync(someBytes, 0, someBytes.Length);
                    if (numRead == 0)
                    {
                        Debug.Log("Client disconnected");
                        break;
                    }
                    string s = Encoding.ASCII.GetString(someBytes, 0, numRead);
                    Debug.Log("FAKE VOIP RECEIVED: " + s);
                    await client.GetStream().WriteAsync(someBytes, 0, numRead);
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Failed to received from client: " + ex.Message + " " + ex.StackTrace);
                 
            }
        }

        void ClientGUI()
        {
            GUI.Label(new Rect(10, 10, 100, 25), "Host IP:");
            hostIP = GUI.TextField(new Rect(110, 10, 500, 25), hostIP);
            GUI.Label(new Rect(10, 50, 100, 25), "Port:");
            hostPortString = GUI.TextField(new Rect(110, 50, 500, 25), hostPortString);
            bool success = ushort.TryParse(hostPortString, out hostPort);
            if (GUI.Button(new Rect(10, 80, 100, 60), "Connect"))
            {
                //FakeVOIPClient(new IPEndPoint(IPAddress.Parse(hostIP), hostPort));
                CreatePeer();
                peer.InitializeClient(new IPEndPoint(IPAddress.Parse(hostIP), hostPort), OnClientPrepared);
            }
        }

        void ClientWithRouteGUI()
        {
            GUI.TextField(new Rect(110, 10, 500, 25), "Connect to IPv4: " + clientConnectToIPv4.ToString(), "label");
            //GUI.TextField(new Rect(110, 50, 500, 25), "Connect to IPv6: " + clientConnecToIPv6.ToString(), "label");
        }

        //string clientIP;
        void HostGUI()
        {
            GUI.TextField(new Rect(10, 10, 500, 25), "Host IP: " + hostIP, "label");
            GUI.TextField(new Rect(10, 50, 500, 25), "Host Port: " + hostPort.ToString(), "label");

            //GUI.Label(new Rect(10, 60, 100, 25), "Client IP:");
            //clientIP = GUI.TextField(new Rect(110, 60, 500, 25), clientIP);
            //if (GUI.Button(new Rect(10, 130, 100, 60), "Add Permission"))
            //{
            //    turnExtension.SendCreatePermissionRequest(
            //        IPAddress.Parse(clientIP), 
            //        m => { }, 
            //        m  => { }
            //    );
            //}
        }

        void OnHostPrepared(string ip, ushort port)
        {
            // Host can now receive connections
            hostIP = ip;
            hostPort = port;
            menuState = MenuState.HOST;
        }

        void OnClientPrepared(IPEndPoint routeEndPointIPv4, IPEndPoint routeEndPointIPv6)
        {
            clientConnectToIPv4 = routeEndPointIPv4;
            clientConnecToIPv6 = routeEndPointIPv6;
            menuState = MenuState.CLIENT_HAS_ROUTE;
            FakeVOIPClient(routeEndPointIPv6);
        }

        async void FakeVOIPClient(IPEndPoint voipHostAddress)
        {
            Logger.Log("Connecting fake voip client " + voipHostAddress);
            fakeVOIPClient = new TcpClient(voipHostAddress.AddressFamily);
            fakeVOIPClient.Connect(voipHostAddress);
            peer.SetLocalEndPoint((IPEndPoint)fakeVOIPClient.Client.LocalEndPoint);
            byte[] bytesToSend = Encoding.ASCII.GetBytes("Hello World");
            await Task.Delay(250);
            try
            {
                while (fakeVOIPClient.Connected)
                {
                    await fakeVOIPClient.GetStream().WriteAsync(bytesToSend, 0, bytesToSend.Length);
                    await fakeVOIPClient.GetStream().WriteAsync(bytesToSend, 0, bytesToSend.Length);
                    await fakeVOIPClient.GetStream().WriteAsync(bytesToSend, 0, bytesToSend.Length);
                    Debug.Log("Client sent");
                    byte[] receiveBuffer = new byte[1024];
                    int bytesReceived = await fakeVOIPClient.GetStream().ReadAsync(receiveBuffer, 0, receiveBuffer.Length);
                    string serverResponse = Encoding.ASCII.GetString(receiveBuffer, 0, bytesReceived);
                    Debug.Log("Client received: " + serverResponse);
                    await Task.Delay(100);
                }
            }
            catch { }
            menuState = MenuState.START;
            peer?.Dispose();
            peer = null;
        }
    }
}