namespace DetonatorAgent.Services;

/// <summary>
/// Utility class for XOR encoding/decoding operations
/// </summary>
public static class XorDecoder {
    /// <summary>
    /// XOR decodes a byte array with the specified key in chunks to avoid memory issues
    /// </summary>
    /// <param name="content">The byte array to decode</param>
    /// <param name="xorKey">The XOR key (0-255)</param>
    /// <param name="chunkSize">Size of chunks to process (default 8KB)</param>
    /// <returns>The decoded byte array</returns>
    public static byte[] Decode(byte[] content, byte xorKey, int chunkSize = 8192) {
        if (content == null || content.Length == 0) {
            return content ?? Array.Empty<byte>();
        }

        var decoded = new byte[content.Length];
        
        for (int i = 0; i < content.Length; i += chunkSize) {
            int currentChunkSize = Math.Min(chunkSize, content.Length - i);
            for (int j = 0; j < currentChunkSize; j++) {
                decoded[i + j] = (byte)(content[i + j] ^ xorKey);
            }
        }
        
        return decoded;
    }
}
