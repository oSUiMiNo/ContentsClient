using Mirror;
using NobleConnect.Mirror;
using TMPro;
using UnityEngine;
namespace NobleConnect.Examples.Mirror
{
    public class ExampleMirrorNetworkManagerGUI : MonoBehaviour
    {
        public GameObject startMenu;
        public GameObject hostMenu;
        public GameObject clientMenu;
        public GameObject clientConnectingMenu;

        public TMP_InputField hostRegionBox;
        public TMP_InputField hostAddressDisplayBox;
        public TMP_InputField hostPortDisplayBox;

        public TMP_InputField hostAddressBox;
        public TMP_InputField hostPortBox;

        public TMP_InputField clientRegionBox;
        public TMP_InputField clientConnectionTypeBox;

        enum MenuState { START, CLIENT, HOST, CLIENT_CONNECTING }

        // The NetworkManager controlled by this GUI
        NobleNetworkManager networkManager;

        // The current menu state
        MenuState menuState = MenuState.START;

        // Get a reference to the NetworkManager
        public void Start()
        {
            // Cast from Unity's NetworkManager to a NobleNetworkManager.
            networkManager = (NobleNetworkManager)NetworkManager.singleton;
        }

        public void OnHostButtonPressed()
        {
            menuState = MenuState.HOST;
            startMenu.SetActive(false);
            hostMenu.SetActive(true);

            networkManager.StartHost();
        }

        public void OnClientButtonPressed()
        {
            menuState = MenuState.CLIENT;
            startMenu.SetActive(false);
            clientMenu.SetActive(true);
        }

        public void OnClientConnectButtonPressed()
        {
            menuState = MenuState.CLIENT_CONNECTING;
            clientMenu.SetActive(false);
            clientConnectingMenu.SetActive(true);

            networkManager.networkAddress = hostAddressBox.text;
            networkManager.networkPort = ushort.Parse(hostPortBox.text);
            networkManager.StartClient();
        }

        public void OnDisconnectButtonPressed()
        {
            menuState = MenuState.START;
            clientMenu.SetActive(false);
            clientConnectingMenu.SetActive(false);
            hostMenu.SetActive(false);
            startMenu.SetActive(true);

            networkManager.StopHost();
        }

        public void OnBackButtonPressed()
        {
            menuState = MenuState.START;
            clientMenu.SetActive(false);
            startMenu.SetActive(true);
        }

        public void Update()
        {
            if (menuState == MenuState.HOST)
            {
                if (networkManager.HostEndPoint == null)
                {
                    // Display host status while initializing
                    if (NobleServer.GetConnectedRegion() == GeographicRegion.AUTO)
                    {
                        hostRegionBox.text = "Selecting region..";
                    }
                    else
                    {
                        hostRegionBox.text = NobleServer.GetConnectedRegion().ToString();

                        hostAddressDisplayBox.text = "Acquiring..";
                        hostPortDisplayBox.text = "Acquiring..";
                    }
                }
                else
                {
                    hostAddressDisplayBox.text = networkManager.HostEndPoint.Address.ToString();
                    hostPortDisplayBox.text = networkManager.HostEndPoint.Port.ToString();
                }
            }
            else if (menuState == MenuState.CLIENT_CONNECTING)
            {
                if (!networkManager.client.isConnecting && !networkManager.client.isConnected)
                {
                    OnDisconnectButtonPressed();
                }
                else
                {
                    if (networkManager.client.GetConnectedRegion() == GeographicRegion.AUTO)
                    {
                        clientRegionBox.text = "Selecting region..";
                    }
                    else
                    {
                        clientRegionBox.text = networkManager.client.GetConnectedRegion().ToString();
                    }

                    if (networkManager.client.isConnecting)
                    {
                        clientConnectionTypeBox.text = "Connecting...";
                    }
                    else
                    {
                        clientConnectionTypeBox.text = networkManager.client.latestConnectionType.ToString();
                    }
                }

            }
        }
    }
}