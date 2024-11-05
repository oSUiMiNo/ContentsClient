using UnityEngine;
using Mirror;
using System.IO;
using System.Collections.Generic;

public class BinaryDataReceiver : NetworkBehaviour
{
    // 受信したファイルを保存するパスをInspectorで設定可能
    [SerializeField]
    private string saveFilePath = "Assets/Client/def4f6a4d582de6ac440e4db72dd377f.shiji";

    private Dictionary<int, byte[]> receivedChunks = new Dictionary<int, byte[]>();


    private static BinaryDataReceiver instance;
    void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }


    private void Start()
    {
        InputEventHandler.OnDown_R += () => Debug.Log($"クライアントスタート");
        InputEventHandler.OnDown_R += () => A();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        Debug.Log($"クライアントスタート");

        // メッセージハンドラの登録
        NetworkClient.RegisterHandler<BinaryDataMessage>(OnReceiveBinaryData);
        NetworkClient.RegisterHandler<ChunkedDataMessage>(OnReceiveChunkedData);

        // サーバーにデータ要求を送信
        SendDataRequest();
    }

    private void A()
    {
        Debug.Log($"クライアントスタート");

        // メッセージハンドラの登録
        NetworkClient.RegisterHandler<BinaryDataMessage>(OnReceiveBinaryData);
        NetworkClient.RegisterHandler<ChunkedDataMessage>(OnReceiveChunkedData);

        // サーバーにデータ要求を送信
        SendDataRequest();
    }

    // サーバーにデータ要求を送信
    private void SendDataRequest()
    {
        BinaryDataRequestMessage requestMessage = new BinaryDataRequestMessage();
        NetworkClient.Send(requestMessage);
        Debug.Log("データ要求メッセージを送信しました。");
    }

    // バイナリデータを受信
    private void OnReceiveBinaryData(BinaryDataMessage msg)
    {
        Debug.Log($"バイナリ受信 {msg}");
        SaveDataToFile(msg.data);
    }

    // チャンクデータを受信
    private void OnReceiveChunkedData(ChunkedDataMessage msg)
    {
        Debug.Log($"チャンク受信 {msg}");
        receivedChunks[msg.chunkIndex] = msg.data;

        // すべてのチャンクを受信したか確認
        if (receivedChunks.Count == msg.totalChunks)
        {
            List<byte> fullData = new List<byte>();
            for (int i = 0; i < msg.totalChunks; i++)
            {
                fullData.AddRange(receivedChunks[i]);
            }

            byte[] completeData = fullData.ToArray();
            SaveDataToFile(completeData);
            receivedChunks.Clear();
        }
    }

    // データをファイルに保存
    private void SaveDataToFile(byte[] data)
    {
        File.WriteAllBytes(saveFilePath, data);
        Debug.Log($"データをファイルに保存しました: {saveFilePath}");
    }
}
