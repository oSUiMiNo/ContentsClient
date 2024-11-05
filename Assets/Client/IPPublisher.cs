using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Cysharp.Threading.Tasks;
using Mirror;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NobleConnect;
using NobleConnect.Mirror;
using UniRx;
using UnityEngine;
using UnityEngine.Networking;

// このNotionページにDB置いてある。
//https://www.notion.so/6795dd70753a495897cb85e0abce95fe

public class IPPublisher : MonoBehaviour
{
    private const string NotionAccessToken = "secret_OIxSWO69mxnD9FNbmL2US0pcsLWCUmsaglBZBCWPWrC"; //新しい方

    NobleNetworkManager networkManager;
    public string hostIP = "";
    public string hostPort = "";
    //public bool isHost, isClient, isOffLine;
    public NetworkRole networkRole  = NetworkRole.Default;
    
    public static IObservable<Unit> On_NetworkRoleSet => on_NetworkRoleSet;
    static Subject<Unit> on_NetworkRoleSet = new Subject<Unit>();
    
    public static IObservable<Unit> On_GetIP => on_GetIP;
    static Subject<Unit> on_GetIP = new Subject<Unit>();
    public BoolReactiveProperty existHostEndPoint = new BoolReactiveProperty(false);

    // GUIプロパティ
    GUIStyle style_Button;
    GUIStyle style_Label;
    GUIStyle style_TextField;
    [SerializeField] int uiSize = 1;
    [SerializeField] Vector2 uiPosition = new Vector2(0, 0);



    async void Start()
    {
        networkManager = (NobleNetworkManager)NetworkManager.singleton;

        // IPが取得出来たらNotionデータベースに送信
        existHostEndPoint.Subscribe(value => 
        {
            if (value == false) return;
            on_GetIP.OnNext(Unit.Default);
        });
        on_GetIP.Subscribe(async _ => await UseAPI_Update());

        InputEventHandler.OnDown_B += async () => await UseAPI_POST();
    }



    async UniTask UseAPI_Update()
    {
        string PageID = "62c3ee9f24244be48eddf8328c727ca7";
        
        //Payloadの準備
        JObject payloadObj = null;
        using (var sr = new StreamReader($@"{Application.dataPath}\Payload.json", System.Text.Encoding.UTF8))
        {
            payloadObj = JObject.Parse(sr.ReadToEnd());
        }
        string ip = networkManager.HostEndPoint.Address.ToString();
        string port = networkManager.HostEndPoint.Port.ToString();

        //payloadObj["parent"]["database_id"] = DatabaseID;
        payloadObj["properties"]["Port"]["rich_text"][0]["text"]["content"] = port;
        payloadObj["properties"]["IP"]["rich_text"][0]["text"]["content"] = ip;
        payloadObj["properties"]["DayTime"]["title"][0]["text"]["content"] = DateTime.Now.ToString();
        string payload = JsonConvert.SerializeObject(payloadObj);


     
        UnityWebRequest request = new UnityWebRequest($"https://api.notion.com/v1/pages/{PageID}", "PATCH");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(payload);
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Authorization", $"Bearer {NotionAccessToken}");
        request.SetRequestHeader("Content-Type", "application/json; charset=UTF-8");
        request.SetRequestHeader("Accept", "application/json");
        request.SetRequestHeader("Notion-Version", "2022-02-22");
        
        await request.SendWebRequest();

        switch (request.result)
        {
            case UnityWebRequest.Result.InProgress:
                Debug.Log("リクエスト中");
                break;

            case UnityWebRequest.Result.Success:
                Debug.Log("リクエスト成功");
                break;

            case UnityWebRequest.Result.ConnectionError:
                Debug.Log(
                    @"サーバとの通信に失敗。
                        リクエストが接続できなかった、
                        セキュリティで保護されたチャネルを確立できなかったなど。");
                Debug.LogError(request.error);
                break;

            case UnityWebRequest.Result.ProtocolError:
                Debug.Log(
                    @"サーバがエラー応答を返した。
                        サーバとの通信には成功したが、
                        接続プロトコルで定義されているエラーを受け取った。");
                Debug.LogError(request.error);
                break;

            case UnityWebRequest.Result.DataProcessingError:
                Debug.Log(
                    @"データの処理中にエラーが発生。
                        リクエストはサーバとの通信に成功したが、
                        受信したデータの処理中にエラーが発生。
                        データが破損しているか、正しい形式ではないなど。");
                Debug.LogError(request.error);
                break;

            default: throw new ArgumentOutOfRangeException();
        }
    }


    async UniTask<HostInfo> UseAPI_POST()
    {
        string DatabaseID = "0dae954ed2204a2a9e1a0f7fe85f9ae2";
        WWWForm form = new WWWForm();

        string jsonStr = string.Empty;
        using (UnityWebRequest request = UnityWebRequest.Post($"https://api.notion.com/v1/databases/{DatabaseID}/query", form))
        {
            request.SetRequestHeader("Authorization", $"Bearer {NotionAccessToken}");
            request.SetRequestHeader("Content-Type", "application/json; charset=UTF-8");
            request.SetRequestHeader("Notion-Version", "2022-02-22");

            await request.SendWebRequest();

            switch (request.result)
            {
                case UnityWebRequest.Result.InProgress:
                    Debug.Log("リクエスト中");
                    break;

                case UnityWebRequest.Result.Success:
                    Debug.Log("リクエスト成功");
                    break;

                case UnityWebRequest.Result.ConnectionError:
                    Debug.Log(
                        @"サーバとの通信に失敗。
                        リクエストが接続できなかった、
                        セキュリティで保護されたチャネルを確立できなかったなど。");
                    Debug.LogError(request.error);
                    break;

                case UnityWebRequest.Result.ProtocolError:
                    Debug.Log(
                        @"サーバがエラー応答を返した。
                        サーバとの通信には成功したが、
                        接続プロトコルで定義されているエラーを受け取った。");
                    Debug.LogError(request.error);
                    break;

                case UnityWebRequest.Result.DataProcessingError:
                    Debug.Log(
                        @"データの処理中にエラーが発生。
                        リクエストはサーバとの通信に成功したが、
                        受信したデータの処理中にエラーが発生。
                        データが破損しているか、正しい形式ではないなど。");
                    Debug.LogError(request.error);
                    break;

                default: throw new ArgumentOutOfRangeException();
            }

            jsonStr = request.downloadHandler.text;
        }

        JObject responseObj = JObject.Parse(jsonStr);

        // レスポンスの中からresultsプロパティの中身を使う
        return new HostInfo()
        {
            IP = responseObj["results"][0]["properties"]["IP"]["rich_text"][0]["text"]["content"].ToString(),
            Port = responseObj["results"][0]["properties"]["Port"]["rich_text"][0]["text"]["content"].ToString()
        };
    }



    // Draw the GUI
    private async void OnGUI()
    {
        style_Button = new GUIStyle("button");
        style_Button.fontSize = 40 * uiSize;

        style_Label = new GUIStyle("label");
        style_Label.fontSize = 40 * uiSize;

        style_TextField = new GUIStyle("textfield");
        style_TextField.fontSize = 40 * uiSize;

        //if (!isHost && !isClient)
        if (networkRole == NetworkRole.Default)
        {
            // Host button
            if (GUI.Button(new Rect(20 + uiPosition.x, 30 + uiPosition.y, 150 * uiSize, 50 * uiSize), "Host", style_Button))
            {
                //isHost = true;
                //isClient = false;
                networkRole = NetworkRole.Host;
                networkManager.StartHost();
                on_NetworkRoleSet.OnNext(Unit.Default);
            }

            // Client button
            if (GUI.Button(new Rect(20 + uiPosition.x, 90 + uiPosition.y, 150 * uiSize, 50 * uiSize), "Client", style_Button))
            {
                networkManager.InitClient();
                networkRole = NetworkRole.Client;
                on_NetworkRoleSet.OnNext(Unit.Default);
                
                // notion からIPとPortを取得
                HostInfo hostInfo = await UseAPI_POST();
                hostIP = hostInfo.IP;
                hostPort = hostInfo.Port;
                networkManager.networkAddress = hostIP;
                networkManager.networkPort = ushort.Parse(hostPort);
                //isHost = false;
                //isClient = true;
            }

            //// OffLine button
            //if (GUI.Button(new Rect(20, 150, 150, 50), "OffLine"))
            //{
            //    //Instantiate(networkManager.playerPrefab);
            //    new GameObject("W(Clone)").AddComponent<WebLSL>();
            //    on_NetworkRoleSet.OnNext(Unit.Default);
            //}
        }
        else
        {
            //if (isHost) GUIHost();
            //else if (isClient) GUIClient();
            // Host or client GUI
            if (networkRole == NetworkRole.Host) GUIHost();
            else if (networkRole == NetworkRole.Client) GUIClient();
        }
    }

    // Draw the host GUI
    void GUIHost()
    {
        existHostEndPoint.Value = networkManager.HostEndPoint != null;
        if (!existHostEndPoint.Value)
        {
            // Display host status while initializing
            if (NobleServer.GetConnectedRegion() == GeographicRegion.AUTO)
            {
                GUI.Label(new Rect(15, 30, 600, 50), "Selecting region..", style_Label);
            }
            else
            {
                GUI.Label(new Rect(15, 30, 600, 50), "Acquiring host address..", style_Label);
            }
        }
        else
        {
            // Display host address, port, and region
            GUI.Label(new Rect(15, 30, 190, 50), "Host IP", style_Label);
            GUI.TextField(new Rect(210, 30, 600, 50), networkManager.HostEndPoint.Address.ToString(), style_Label);
            GUI.Label(new Rect(15, 90, 190, 50), "Host Port:", style_Label);
            GUI.TextField(new Rect(210, 90, 600, 50), networkManager.HostEndPoint.Port.ToString(), style_Label);
            GUI.Label(new Rect(15, 150, 190, 50), "Region:", style_Label);
            GUI.TextField(new Rect(210, 150, 600, 50), NobleServer.GetConnectedRegion().ToString(), style_Label);
        }

        // Disconnect Button
        if (GUI.Button(new Rect(15, 210, 250, 50), "Disconnect", style_Button))
        {
            networkManager.StopHost();
            //isHost = false;
            networkRole = NetworkRole.Default;
        }

        //if (!NobleServer.active) isHost = false;
        if (!NobleServer.active) networkRole = NetworkRole.Default;
    }

    // Draw the client GUI
    async void GUIClient()
    {
        if (!networkManager.isNetworkActive)
        {
            // Text boxes for entering host's address
            GUI.Label(new Rect(15, 30, 190, 50), "Host IP:", style_Label);
            hostIP = GUI.TextField(new Rect(210, 30, 600, 50), hostIP, style_TextField);
            GUI.Label(new Rect(15, 90, 190, 50), "Host Port:", style_Label);
            hostPort = GUI.TextField(new Rect(210, 90, 600, 50), hostPort, style_TextField);

            // Connect button
            if (GUI.Button(new Rect(15, 210, 220, 50), "Connect", style_Button))
            {
                //HostInfo hostInfo = await UseAPI_POST();
                //hostIP = hostInfo.IP;
                //hostPort = hostInfo.Port;
                //networkManager.networkAddress = hostIP;
                //networkManager.networkPort = ushort.Parse(hostPort);
                networkManager.StartClient();
            }

            // Back button
            if (GUI.Button(new Rect(250, 210, 220, 50), "Back", style_Button))
            {
                //isClient = false;
                networkRole = NetworkRole.Default;
            }
        }
        else if (networkManager.client != null)
        {
            // Disconnect button
            GUI.Label(new Rect(10, 10, 300, 22), "Connection type: " + networkManager.client.latestConnectionType);
            GUI.Label(new Rect(10, 37, 300, 22), "Region: " + networkManager.client.GetConnectedRegion());
            if (GUI.Button(new Rect(10, 64, 110, 30), "Disconnect"))
            {
                if (networkManager.client.isConnected)
                {
                    // If we are already connected it is best to quit gracefully by sending
                    // a disconnect message to the host.
                    networkManager.client.Disconnect();
                }
                else
                {
                    // If the connection is still in progress StopClient will cancel it
                    networkManager.StopClient();
                }
                //isClient = false;
                networkRole = NetworkRole.Default;
            }
        }
    }
}

public enum NetworkRole
{
    Host,
    Client,
    //OffLine,
    Default
}

public class HostInfo
{
    public string Port { get; set; }
    public string IP { get; set; }
}























//async UniTask<JObject> UseAPI_POST()
//{
//    string DatabaseID = "0dae954ed2204a2a9e1a0f7fe85f9ae2";
//    WWWForm form = new WWWForm();

//    string jsonStr = string.Empty;
//    using (UnityWebRequest request = UnityWebRequest.Post($"https://api.notion.com/v1/databases/{DatabaseID}/query", form))
//    {
//        request.SetRequestHeader("Authorization", $"Bearer {NotionAccessToken}");
//        request.SetRequestHeader("Content-Type", "application/json; charset=UTF-8");
//        request.SetRequestHeader("Notion-Version", "2022-02-22");

//        await request.SendWebRequest();

//        switch (request.result)
//        {
//            case UnityWebRequest.Result.InProgress:
//                Debug.Log("リクエスト中");
//                break;

//            case UnityWebRequest.Result.Success:
//                Debug.Log("リクエスト成功");
//                break;

//            case UnityWebRequest.Result.ConnectionError:
//                Debug.Log(
//                    @"サーバとの通信に失敗。
//                        リクエストが接続できなかった、
//                        セキュリティで保護されたチャネルを確立できなかったなど。");
//                Debug.LogError(request.error);
//                break;

//            case UnityWebRequest.Result.ProtocolError:
//                Debug.Log(
//                    @"サーバがエラー応答を返した。
//                        サーバとの通信には成功したが、
//                        接続プロトコルで定義されているエラーを受け取った。");
//                Debug.LogError(request.error);
//                break;

//            case UnityWebRequest.Result.DataProcessingError:
//                Debug.Log(
//                    @"データの処理中にエラーが発生。
//                        リクエストはサーバとの通信に成功したが、
//                        受信したデータの処理中にエラーが発生。
//                        データが破損しているか、正しい形式ではないなど。");
//                Debug.LogError(request.error);
//                break;

//            default: throw new ArgumentOutOfRangeException();
//        }

//        jsonStr = request.downloadHandler.text;
//    }

//    JObject responseObj = JObject.Parse(jsonStr);

//    // レスポンスの中からresultsプロパティの中身を使う
//    Debug.Log(responseObj["results"]);

//    foreach (var a in responseObj["results"])
//    {
//        Debug.Log(a["properties"]);
//    }

//    return responseObj;
//}



//async UniTask UseAPI_Add_Test()
//{
//    string DatabaseID = "0dae954ed2204a2a9e1a0f7fe85f9ae2";
//    //EditableJSON eJson_Payload = new EditableJSON($@"{Application.dataPath}\Payload.json");
//    //eJson_Payload.Obj["parent"]["database_id"] = DatabaseID;

//    //string payload = eJson_Payload.Json;
//    //string payload = JsonConvert.SerializeObject(eJson_Payload.obj, Formatting.Indented);
//    JObject payloadObj = null;
//    using (var sr = new StreamReader($@"{Application.dataPath}\Payload.json", System.Text.Encoding.UTF8))
//    {
//        payloadObj = JObject.Parse(sr.ReadToEnd());
//        Debug.Log(payloadObj);
//    }
//    payloadObj["parent"]["database_id"] = DatabaseID;
//    string payload = JsonConvert.SerializeObject(payloadObj);
//    DebugView.Log($"{payload}");


//    UnityWebRequest request = UnityWebRequest.PostWwwForm($"https://api.notion.com/v1/pages", payload);
//    request.SetRequestHeader("Authorization", $"Bearer {NotionAccessToken}");
//    request.SetRequestHeader("Content-Type", "application/json; charset=UTF-8");
//    request.SetRequestHeader("Accept", "application/json");
//    request.SetRequestHeader("Notion-Version", "2022-02-22");

//    byte[] bodyRaw = Encoding.UTF8.GetBytes(payload);
//    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
//    request.downloadHandler = new DownloadHandlerBuffer();


//    await request.SendWebRequest();



//    switch (request.result)
//    {
//        case UnityWebRequest.Result.InProgress:
//            Debug.Log("リクエスト中");
//            break;

//        case UnityWebRequest.Result.Success:
//            Debug.Log("リクエスト成功");
//            break;

//        case UnityWebRequest.Result.ConnectionError:
//            Debug.Log(
//                @"サーバとの通信に失敗。
//                        リクエストが接続できなかった、
//                        セキュリティで保護されたチャネルを確立できなかったなど。");
//            Debug.LogError(request.error);
//            break;

//        case UnityWebRequest.Result.ProtocolError:
//            Debug.Log(
//                @"サーバがエラー応答を返した。
//                        サーバとの通信には成功したが、
//                        接続プロトコルで定義されているエラーを受け取った。");
//            Debug.LogError(request.error);
//            break;

//        case UnityWebRequest.Result.DataProcessingError:
//            Debug.Log(
//                @"データの処理中にエラーが発生。
//                        リクエストはサーバとの通信に成功したが、
//                        受信したデータの処理中にエラーが発生。
//                        データが破損しているか、正しい形式ではないなど。");
//            Debug.LogError(request.error);
//            break;

//        default: throw new ArgumentOutOfRangeException();
//    }
//}