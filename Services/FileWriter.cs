namespace DetonatorAgent.Services;

public static class FileWriter {
    /// Writes a byte array to a file, optionally XOR decoding it byte by byte during the write process
    public static FileWriteResult Write(string filePath, byte[] content, byte? xorKey = null) {
        try {
            if (content == null || content.Length == 0) {
                return new FileWriteResult(FileWriteStatus.Failed, "Content to write cannot be null or empty.");
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

            // Real-time protection can quarantine the file while it is being
            // written or immediately after the write handle is closed.
            if (!File.Exists(filePath)) {
                return new FileWriteResult(FileWriteStatus.Detected, "The file was removed while it was being written.");
            }

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length != content.Length) {
                return new FileWriteResult(FileWriteStatus.Detected, "The file was modified while it was being written.");
            }

            return new FileWriteResult(FileWriteStatus.Written);
        }
        catch (IOException ex) {
            // An I/O failure does not necessarily mean that antivirus detected
            // the file; it could also be a permission, storage, or sharing issue.
            return new FileWriteResult(FileWriteStatus.Failed, ex.Message);
        }
    }
}
