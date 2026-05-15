using System.Runtime.Serialization;
using System.ServiceModel;

namespace ParallelTaskHub.SoapClient;

[DataContract(Namespace = "http://schemas.datacontract.org/2004/07/")]
public class MatrixInitRequest
{
    [DataMember] public int TotalRows { get; set; }
    [DataMember] public int TotalCols { get; set; }
    [DataMember] public int ExpectedChunks { get; set; }
}

// NAPRAWA: int[][] nie jest obsługiwane przez DataContractSerializer w .NET 10 preview
// (CoreWCF rzuca fault przy deserializacji tablicy postrzępionej).
// Zamiast tego: płaska tablica int[] + wymiary ChunkRows/ChunkCols.
// Serwer rekonstruuje int[][] w bramce SOAP przed przekazaniem do silnika.
[DataContract(Namespace = "http://schemas.datacontract.org/2004/07/")]
public class MatrixChunkRequest
{
    [DataMember] public string SessionId { get; set; } = string.Empty;
    [DataMember] public int ChunkIndex { get; set; }
    [DataMember] public int ChunkRows { get; set; }
    [DataMember] public int ChunkCols { get; set; }
    [DataMember] public int[] RowsFlat { get; set; } = Array.Empty<int>();
}

[DataContract(Namespace = "http://schemas.datacontract.org/2004/07/")]
public class MultiCalculationRequest
{
    [DataMember] public string IdA { get; set; } = string.Empty;
    [DataMember] public string IdB { get; set; } = string.Empty;
}

[DataContract(Namespace = "http://schemas.datacontract.org/2004/07/")]
public class SoapChunkResponse
{
    [DataMember] public int ChunkIndex { get; set; }
    [DataMember] public bool Ok { get; set; }
    [DataMember] public bool IsComplete { get; set; }
    [DataMember] public string? FileId { get; set; }
    [DataMember] public string Message { get; set; } = string.Empty;
}

[DataContract(Namespace = "http://schemas.datacontract.org/2004/07/")]
public class DownloadSetupRequest
{
    [DataMember] public string FileId { get; set; } = string.Empty;
}

[DataContract(Namespace = "http://schemas.datacontract.org/2004/07/")]
public class DownloadSetupResponse
{
    [DataMember] public bool Exists { get; set; }
    [DataMember] public long TotalSizeBytes { get; set; }
    [DataMember] public int ExpectedChunks { get; set; }
    [DataMember] public int ChunkSize { get; set; }
    [DataMember] public string Message { get; set; } = string.Empty;
}

[DataContract(Namespace = "http://schemas.datacontract.org/2004/07/")]
public class ChunkDataRequest
{
    [DataMember] public string FileId { get; set; } = string.Empty;
    [DataMember] public int ChunkIndex { get; set; }
}

[DataContract(Namespace = "http://schemas.datacontract.org/2004/07/")]
public class ChunkDataResponse
{
    [DataMember] public bool Success { get; set; }
    [DataMember] public byte[] Data { get; set; } = Array.Empty<byte>();
    [DataMember] public bool IsComplete { get; set; }
    [DataMember] public string Message { get; set; } = string.Empty;
}

[DataContract(Namespace = "http://schemas.datacontract.org/2004/07/")]
public class MultiCalcResponse
{
    [DataMember] public bool Success { get; set; }
    [DataMember] public string? ResultFileId { get; set; }
    [DataMember] public string Message { get; set; } = string.Empty;
}

[ServiceContract]
public interface ISoapMatrixEngine
{
    [OperationContract] string InitUploadSoap(MatrixInitRequest request);
    [OperationContract] SoapChunkResponse UploadChunkSoap(MatrixChunkRequest request);
    [OperationContract] MultiCalcResponse MultiplySoap(MultiCalculationRequest request);
    [OperationContract] DownloadSetupResponse InitDownloadSoap(DownloadSetupRequest request);
    [OperationContract] ChunkDataResponse DownloadChunkSoap(ChunkDataRequest request);
}

[DataContract(Namespace = "http://schemas.datacontract.org/2004/07/")]
public class MandelbrotTaskRequest
{
    [DataMember] public int Width { get; set; }
    [DataMember] public int Height { get; set; }
    [DataMember] public int maxThreads { get; set; }
}

[DataContract(Namespace = "http://schemas.datacontract.org/2004/07/")]
public class MandelbrotTaskResponse
{
    [DataMember] public string? FileId { get; set; }
    [DataMember] public string Message { get; set; } = string.Empty;
}

[ServiceContract]
public interface ISoapMandelbrotEngine
{
    [OperationContract] MandelbrotTaskResponse GenerateFractalSoap(MandelbrotTaskRequest request);
    [OperationContract] DownloadSetupResponse InitDownloadFractalSoap(DownloadSetupRequest request);
    [OperationContract] ChunkDataResponse DownloadChunkFractalSoap(ChunkDataRequest request);
}