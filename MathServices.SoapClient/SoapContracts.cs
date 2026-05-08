using System.Runtime.Serialization;
using System.ServiceModel;

namespace MathServices.SoapClient;

[DataContract(Namespace = "http://schemas.datacontract.org/2004/07/")]
public class InitUploadRequest
{
    [DataMember] public int TotalRows { get; set; }
    [DataMember] public int TotalCols { get; set; }
    [DataMember] public int ExpectedChunks { get; set; }
}

[DataContract(Namespace = "http://schemas.datacontract.org/2004/07/")]
public class UploadChunkRequest
{
    [DataMember] public string SessionId { get; set; } = string.Empty;
    [DataMember] public int ChunkIndex { get; set; }
    [DataMember] public int[][] RowsChunk { get; set; } = Array.Empty<int[]>();
}

[DataContract(Namespace = "http://schemas.datacontract.org/2004/07/")]
public class MultiplyRequest
{
    [DataMember] public string IdA { get; set; } = string.Empty;
    [DataMember] public string IdB { get; set; } = string.Empty;
}

[DataContract(Namespace = "http://schemas.datacontract.org/2004/07/")]
public class ChunkResponseModel
{
    [DataMember] public int ChunkIndex { get; set; }
    [DataMember] public bool Ok { get; set; }
    [DataMember] public bool IsComplete { get; set; }
    [DataMember] public string? FileId { get; set; }
    [DataMember] public string Message { get; set; } = string.Empty;
}

[DataContract(Namespace = "http://schemas.datacontract.org/2004/07/")]
public class InitDownloadRequest
{
    [DataMember] public string FileId { get; set; } = string.Empty;
}

[DataContract(Namespace = "http://schemas.datacontract.org/2004/07/")]
public class InitDownloadResponseModel
{
    [DataMember] public bool Exists { get; set; }
    [DataMember] public long TotalSizeBytes { get; set; }
    [DataMember] public int ExpectedChunks { get; set; }
    [DataMember] public int ChunkSize { get; set; } 
    [DataMember] public string Message { get; set; } = string.Empty;
}

[DataContract(Namespace = "http://schemas.datacontract.org/2004/07/")]
public class DownloadChunkRequest
{
    [DataMember] public string FileId { get; set; } = string.Empty;
    [DataMember] public int ChunkIndex { get; set; } 
}

[DataContract(Namespace = "http://schemas.datacontract.org/2004/07/")]
public class DownloadChunkResponseModel
{
    [DataMember] public bool Success { get; set; }
    [DataMember] public byte[] Data { get; set; } = Array.Empty<byte>();
    [DataMember] public bool IsComplete { get; set; }
    [DataMember] public string Message { get; set; } = string.Empty;
}

[DataContract(Namespace = "http://schemas.datacontract.org/2004/07/")]
public class MultiplyResponseModel
{
    [DataMember] public bool Success { get; set; }
    [DataMember] public string? ResultFileId { get; set; }
    [DataMember] public string Message { get; set; } = string.Empty;
}

[ServiceContract]
public interface IMatrixSoapService
{
    [OperationContract] string InitUploadSoap(InitUploadRequest request);
    [OperationContract] ChunkResponseModel UploadChunkSoap(UploadChunkRequest request);
    [OperationContract] MultiplyResponseModel MultiplySoap(MultiplyRequest request);
    [OperationContract] InitDownloadResponseModel InitDownloadSoap(InitDownloadRequest request);
    [OperationContract] DownloadChunkResponseModel DownloadChunkSoap(DownloadChunkRequest request);
}
[DataContract(Namespace = "http://schemas.datacontract.org/2004/07/")]
public class FractalRequest
{
    [DataMember] public int Width { get; set; }
    [DataMember] public int Height { get; set; }
    [DataMember] public int maxThreads { get; set; }
}

[DataContract(Namespace = "http://schemas.datacontract.org/2004/07/")]
public class FractalGenerateResponseModel
{
    [DataMember] public string? FileId { get; set; }
    [DataMember] public string Message { get; set; } = string.Empty;
}

[ServiceContract]
public interface IFractalSoapService
{
    [OperationContract] FractalGenerateResponseModel GenerateFractalSoap(FractalRequest request);
    [OperationContract] InitDownloadResponseModel InitDownloadFractalSoap(InitDownloadRequest request);
    [OperationContract] DownloadChunkResponseModel DownloadChunkFractalSoap(DownloadChunkRequest request);
}