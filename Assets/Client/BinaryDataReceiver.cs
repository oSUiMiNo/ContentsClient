using UnityEngine;
using Mirror;
using System.IO;
using System.Collections.Generic;

public class BinaryDataReceiver : NetworkBehaviour
{
    // ��M�����t�@�C����ۑ�����p�X��Inspector�Őݒ�\
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
        InputEventHandler.OnDown_R += () => Debug.Log($"�N���C�A���g�X�^�[�g");
        InputEventHandler.OnDown_R += () => A();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        Debug.Log($"�N���C�A���g�X�^�[�g");

        // ���b�Z�[�W�n���h���̓o�^
        NetworkClient.RegisterHandler<BinaryDataMessage>(OnReceiveBinaryData);
        NetworkClient.RegisterHandler<ChunkedDataMessage>(OnReceiveChunkedData);

        // �T�[�o�[�Ƀf�[�^�v���𑗐M
        SendDataRequest();
    }

    private void A()
    {
        Debug.Log($"�N���C�A���g�X�^�[�g");

        // ���b�Z�[�W�n���h���̓o�^
        NetworkClient.RegisterHandler<BinaryDataMessage>(OnReceiveBinaryData);
        NetworkClient.RegisterHandler<ChunkedDataMessage>(OnReceiveChunkedData);

        // �T�[�o�[�Ƀf�[�^�v���𑗐M
        SendDataRequest();
    }

    // �T�[�o�[�Ƀf�[�^�v���𑗐M
    private void SendDataRequest()
    {
        BinaryDataRequestMessage requestMessage = new BinaryDataRequestMessage();
        NetworkClient.Send(requestMessage);
        Debug.Log("�f�[�^�v�����b�Z�[�W�𑗐M���܂����B");
    }

    // �o�C�i���f�[�^����M
    private void OnReceiveBinaryData(BinaryDataMessage msg)
    {
        Debug.Log($"�o�C�i����M {msg}");
        SaveDataToFile(msg.data);
    }

    // �`�����N�f�[�^����M
    private void OnReceiveChunkedData(ChunkedDataMessage msg)
    {
        Debug.Log($"�`�����N��M {msg}");
        receivedChunks[msg.chunkIndex] = msg.data;

        // ���ׂẴ`�����N����M�������m�F
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

    // �f�[�^���t�@�C���ɕۑ�
    private void SaveDataToFile(byte[] data)
    {
        File.WriteAllBytes(saveFilePath, data);
        Debug.Log($"�f�[�^���t�@�C���ɕۑ����܂���: {saveFilePath}");
    }
}
