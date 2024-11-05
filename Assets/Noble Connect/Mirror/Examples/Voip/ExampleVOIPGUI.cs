namespace NobleConnect.Examples.Mirror
{
    using System.Net;
    using global::Mirror;
    using TMPro;
    using UnityEngine;

    public class ExampleVOIPGUI : MonoBehaviour
    {

        public GameObject startPanel;
        public GameObject hostPanel;
        public GameObject clientPanel;
        public GameObject connectedPanel;

        public TMP_InputField hostMirrorAddressOnHost;
        public TMP_InputField hostMirrorPortOnHost;
        public TMP_InputField hostVOIPAddressOnHost;
        public TMP_InputField hostVOIPPortOnHost;

        public TMP_InputField hostMirrorAddressOnClient;
        public TMP_InputField hostMirrorPortOnClient;
        public TMP_InputField hostVOIPAddressOnClient;
        public TMP_InputField hostVOIPPortOnClient;

        public TMP_Text voipReceivedOnClient;
        public TMP_Text voipReceivedOnServer;

        ExampleMirrorVOIPNobleNetworkManager netMan;

        private void Start()
        {
            netMan = (ExampleMirrorVOIPNobleNetworkManager)NetworkManager.singleton;
        }

        public void OnHostButtonPressed()
        {
            hostPanel.SetActive(true);
            startPanel.SetActive(false);

            netMan.StartHost();
        }

        public void OnClientButtonPressed()
        {
            clientPanel.SetActive(true);
            startPanel.SetActive(false);

            netMan.InitClient();
        }

        public void OnConnectButtonPressed()
        {
            netMan.networkAddress = hostMirrorAddressOnClient.text;
            netMan.networkPort = ushort.Parse(hostMirrorPortOnClient.text);
            netMan.HostVOIPEndPoint = new IPEndPoint(IPAddress.Parse(hostVOIPAddressOnClient.text), ushort.Parse(hostVOIPPortOnClient.text));
            netMan.StartClient();

            connectedPanel.SetActive(true);
            clientPanel.SetActive(false);
        }

        public void OnDisconnectButtonPressed()
        {
            netMan.StopHost();

            clientPanel.SetActive(false);
            hostPanel.SetActive(false);
            connectedPanel.SetActive(false);
            startPanel.SetActive(true);
        }

        public void OnCancelButtonPressed()
        {
            clientPanel.SetActive(false);
            startPanel.SetActive(true);
        }

        private void Update()
        {
            if (hostPanel.activeSelf)
            {
                hostMirrorAddressOnHost.text = netMan.HostEndPoint?.Address.ToString();
                hostMirrorPortOnHost.text = netMan.HostEndPoint?.Port.ToString();
                hostVOIPAddressOnHost.text = netMan.HostVOIPEndPoint?.Address.ToString();
                hostVOIPPortOnHost.text = netMan.HostVOIPEndPoint?.Port.ToString();
                voipReceivedOnServer.text = netMan.LastFakeVOIPMessage;
            }
            else if (connectedPanel.activeSelf)
            {
                voipReceivedOnClient.text = netMan.LastFakeVOIPMessage;

                if (!netMan.client.isConnected && !netMan.client.isConnecting)
                {
                    OnDisconnectButtonPressed();
                }
            }
        }

    }
}