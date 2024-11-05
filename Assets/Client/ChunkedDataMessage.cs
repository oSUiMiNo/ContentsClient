using Mirror;

public struct ChunkedDataMessage : NetworkMessage
{
    public int chunkIndex;
    public int totalChunks;
    public byte[] data;
}
