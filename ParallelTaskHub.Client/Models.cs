namespace ParallelTaskHub.Client;

public record InitUploadResponse(string SessionId);
public record ChunkResponse(int ChunkIndex, bool Ok, bool IsComplete, string? FileId, string Message);
public record MultiplyResponse(bool Success, string? ResultFileId, string Message);
public record InitDownloadResponse(bool Exists, long TotalSizeBytes, int ExpectedChunks, int ChunkSize, string Message);
public record DownloadChunkResponse(bool Success, byte[] Data, bool IsComplete, string Message);
public record FractalGenerateResponse(string FileId);
public record ErrorMessageResponse(string Message);