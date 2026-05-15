using System.Runtime.Serialization;

namespace ParallelTaskHub.Server.Logic;

// --- ŻĄDANIA (REQUESTS) ---

[DataContract]
public class MatrixInitRequest
{
    [DataMember] public int TotalRows { get; set; }
    [DataMember] public int TotalCols { get; set; }
    [DataMember] public int ExpectedChunks { get; set; }
}

[DataContract]
public class MatrixChunkRequest
{
    [DataMember] public string SessionId { get; set; } = string.Empty;
    [DataMember] public int ChunkIndex { get; set; }
    [DataMember] public int[][] RowsChunk { get; set; } = Array.Empty<int[]>();
}

[DataContract]
public class MultiCalculationRequest
{
    [DataMember] public string IdA { get; set; } = string.Empty;
    [DataMember] public string IdB { get; set; } = string.Empty;
}

[DataContract]
public class DownloadSetupRequest
{
    [DataMember] public string FileId { get; set; } = string.Empty;
}

[DataContract]
public class ChunkDataRequest
{
    [DataMember] public string FileId { get; set; } = string.Empty;
    [DataMember] public int ChunkIndex { get; set; }
}

[DataContract]
public class MandelbrotTaskRequest
{
    [DataMember] public int Width { get; set; }
    [DataMember] public int Height { get; set; }
    [DataMember] public int maxThreads { get; set; }
}

// --- ODPOWIEDZI (RESPONSES) ---

[DataContract]
public class SoapChunkResponse
{
    [DataMember] public int ChunkIndex { get; set; }
    [DataMember] public bool Ok { get; set; }
    [DataMember] public bool IsComplete { get; set; }
    [DataMember] public string? FileId { get; set; }
    [DataMember] public string Message { get; set; } = string.Empty;
}

[DataContract]
public class MultiCalcResponse
{
    [DataMember] public bool Success { get; set; }
    [DataMember] public string? ResultFileId { get; set; }
    [DataMember] public string Message { get; set; } = string.Empty;
}

[DataContract]
public class DownloadSetupResponse
{
    [DataMember] public bool Exists { get; set; }
    [DataMember] public long TotalSizeBytes { get; set; }
    [DataMember] public int ExpectedChunks { get; set; }
    [DataMember] public int ChunkSize { get; set; }
}

[DataContract]
public class ChunkDataResponse
{
    [DataMember] public bool Success { get; set; }
    [DataMember] public byte[] Data { get; set; } = Array.Empty<byte>();
    [DataMember] public bool IsComplete { get; set; }
}

[DataContract]
public class MandelbrotTaskResponse
{
    [DataMember] public string FileId { get; set; } = string.Empty;
    [DataMember] public string Message { get; set; } = string.Empty;
}