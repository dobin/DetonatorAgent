namespace DetonatorAgent.Services;

public static class FileWriter {
    /// Writes a byte array to a file, optionally XOR decoding it byte by byte during the write process
    /// Throws IOException if the file write fails or is detected as quarantined/blocked by AV
    public static void Write(string filePath, byte[] content, byte? xorKey = null) {
        if (content == null || content.Length == 0) {
            throw new ArgumentException("Content to write cannot be null or empty", nameof(content));
        }

        using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: false)) {
            if (xorKey.HasValue) {
                // Write with XOR decoding, one byte at a time
                // With this we make sure MDE doesnt find any (unencrypted) maldware artefacts in memory
                byte[] singleByte = new byte[1];
                for (int i = 0; i < content.Length; i++) {
                    singleByte[0] = (byte)(content[i] ^ xorKey.Value);
                    fileStream.Write(singleByte, 0, 1);
                }
            }
            else {
                // No XOR decoding needed, write directly
                fileStream.Write(content, 0, content.Length);
            }
        }

        // Verify the file was successfully written and not quarantined by AV
        // This is not a good test and not reliable
        if (!File.Exists(filePath)) {
            throw new IOException($"Failed to write file: {filePath} - File does not exist after write operation (likely quarantined by antivirus)");
        }

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length != content.Length) {
            throw new IOException($"Failed to write file: {filePath} - Expected {content.Length} bytes but got {fileInfo.Length} bytes (file may have been truncated or quarantined)");
        }
    }
}
