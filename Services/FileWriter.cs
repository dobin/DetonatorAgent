namespace DetonatorAgent.Services;

/// <summary>
/// Utility class for writing files with optional XOR decoding
/// </summary>
public static class FileWriter {
    /// <summary>
    /// Writes a byte array to a file, optionally XOR decoding it byte by byte during the write process
    /// </summary>
    /// <param name="filePath">The path where the file should be written</param>
    /// <param name="content">The byte array to write</param>
    /// <param name="xorKey">Optional XOR key for decoding (0-255)</param>
    /// <returns>Task representing the async operation</returns>
    public static async Task WriteAsync(string filePath, byte[] content, byte? xorKey = null) {
        if (content == null || content.Length == 0) {
            await File.WriteAllBytesAsync(filePath, Array.Empty<byte>());
            return;
        }

        using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true)) {
            if (xorKey.HasValue) {
                // Write with XOR decoding, one byte at a time
                // With this we make sure MDE doesnt find any (unencrypted) maldware artefacts in memory
                byte[] singleByte = new byte[1];
                for (int i = 0; i < content.Length; i++) {
                    singleByte[0] = (byte)(content[i] ^ xorKey.Value);
                    await fileStream.WriteAsync(singleByte, 0, 1);
                }
            }
            else {
                // No XOR decoding needed, write directly
                await fileStream.WriteAsync(content, 0, content.Length);
            }
        }
    }
}
