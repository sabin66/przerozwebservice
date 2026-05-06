using CoreWCF;
using CoreWCF.Configuration;
using CoreWCF.Description;
using System.Runtime.Serialization;
using MathServices.Logic;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddServiceModelServices();
builder.Services.AddServiceModelMetadata();
builder.Services.AddSingleton<IServiceBehavior, UseRequestHeadersForMetadataAddressBehavior>();

builder.Services.AddSingleton<MatrixLogic>(); 

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}


app.MapPost("/api/matrix/upload/init", (InitUploadRequest req, MatrixLogic logic) =>
{
    string sessionId = logic.InitUpload(req.TotalRows, req.TotalCols, req.ExpectedChunks);
    return Results.Ok(new { SessionId = sessionId });
});

app.MapPost("/api/matrix/upload/chunk", (UploadChunkRequest req, MatrixLogic logic) =>
{
    try
    {
        var logicResponse = logic.UploadChunk(req.SessionId, req.ChunkIndex, req.RowsChunk);
        var response = new ChunkResponseModel
        {
            ChunkIndex = logicResponse.ChunkIndex,
            Ok = logicResponse.Ok,
            IsComplete = logicResponse.IsComplete,
            FileId = logicResponse.FileId,
            Message = logicResponse.Message
        };
        if (!response.Ok) return Results.BadRequest(response);
        return Results.Ok(response);
    }
    catch (Exception ex) { return Results.Problem(ex.Message); }
});

app.MapPost("/api/matrix/multiply", (MultiplyRequest req, MatrixLogic logic) =>
{
    var result = logic.MultiplyMatrices(req.IdA, req.IdB);
    if (!result.Success)
        return Results.BadRequest(new { Success = false, Message = result.Message });

    return Results.Ok(new { Success = true, ResultFileId = result.ResultFileId, Message = result.Message });
});

app.MapGet("/api/matrix/download/init/{fileId}", (string fileId, MatrixLogic logic) =>
{
    var logicResponse = logic.InitDownload(fileId);

    if (!logicResponse.Exists)
        return Results.NotFound(new { Message = logicResponse.Message });

    var response = new InitDownloadResponseModel
    {
        Exists = logicResponse.Exists,
        TotalSizeBytes = logicResponse.TotalSizeBytes,
        ExpectedChunks = logicResponse.ExpectedChunks,
        ChunkSize = logicResponse.ChunkSize,
        Message = logicResponse.Message
    };

    return Results.Ok(response);
});

app.MapGet("/api/matrix/download/chunk/{fileId}/{chunkIndex:int}", (string fileId, int chunkIndex, MatrixLogic logic) =>
{
    var logicResponse = logic.DownloadChunk(fileId, chunkIndex);
    if (!logicResponse.Success)
        return Results.NotFound(new { Message = logicResponse.Message });

    var response = new DownloadChunkResponseModel
    {
        Success = logicResponse.Success,
        Data = logicResponse.Data,
        IsComplete = logicResponse.IsComplete,
        Message = logicResponse.Message
    };
    return Results.Ok(response);
});


app.UseServiceModel(serviceBuilder =>
{
    serviceBuilder.AddService<MatrixSoapService>(serviceOptions => { });
    serviceBuilder.AddServiceEndpoint<MatrixSoapService, IMatrixSoapService>(new BasicHttpBinding(), "/MatrixSoap.svc");
    
    var serviceMetadataBehavior = app.Services.GetRequiredService<ServiceMetadataBehavior>();
    serviceMetadataBehavior.HttpGetEnabled = true;
});

app.Run();


[DataContract]
public class InitUploadRequest
{
    [DataMember] public int TotalRows { get; set; }
    [DataMember] public int TotalCols { get; set; }
    [DataMember] public int ExpectedChunks { get; set; }
}

[DataContract]
public class UploadChunkRequest
{
    [DataMember] public string SessionId { get; set; } = string.Empty;
    [DataMember] public int ChunkIndex { get; set; }
    [DataMember] public int[][] RowsChunk { get; set; } = Array.Empty<int[]>();
}

[DataContract]
public class MultiplyRequest
{
    [DataMember] public string IdA { get; set; } = string.Empty;
    [DataMember] public string IdB { get; set; } = string.Empty;
}

[DataContract]
public class ChunkResponseModel
{
    [DataMember] public int ChunkIndex { get; set; }
    [DataMember] public bool Ok { get; set; }
    [DataMember] public bool IsComplete { get; set; }
    [DataMember] public string? FileId { get; set; }
    [DataMember] public string Message { get; set; } = string.Empty;
}

[DataContract]
public class InitDownloadRequest
{
    [DataMember] public string FileId { get; set; } = string.Empty;
}

[DataContract]
public class InitDownloadResponseModel
{
    [DataMember] public bool Exists { get; set; }
    [DataMember] public long TotalSizeBytes { get; set; }
    [DataMember] public int ExpectedChunks { get; set; }
    [DataMember] public int ChunkSize { get; set; } 
    [DataMember] public string Message { get; set; } = string.Empty;
}

[DataContract]
public class DownloadChunkRequest
{
    [DataMember] public string FileId { get; set; } = string.Empty;
    [DataMember] public int ChunkIndex { get; set; } 
}

[DataContract]
public class DownloadChunkResponseModel
{
    [DataMember] public bool Success { get; set; }
    [DataMember] public byte[] Data { get; set; } = Array.Empty<byte>();
    [DataMember] public bool IsComplete { get; set; }
    [DataMember] public string Message { get; set; } = string.Empty;
}

[DataContract]
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

public class MatrixSoapService : IMatrixSoapService
{
    private readonly MatrixLogic _logic;

    public MatrixSoapService(MatrixLogic logic) { _logic = logic; }

    public string InitUploadSoap(InitUploadRequest req)
    {
        return _logic.InitUpload(req.TotalRows, req.TotalCols, req.ExpectedChunks);
    }

    public ChunkResponseModel UploadChunkSoap(UploadChunkRequest req)
    {
        var logicResponse = _logic.UploadChunk(req.SessionId, req.ChunkIndex, req.RowsChunk);
        return new ChunkResponseModel
        {
            ChunkIndex = logicResponse.ChunkIndex,
            Ok = logicResponse.Ok,
            IsComplete = logicResponse.IsComplete,
            FileId = logicResponse.FileId,
            Message = logicResponse.Message
        };
    }

    public MultiplyResponseModel MultiplySoap(MultiplyRequest req)
    {
        var result = _logic.MultiplyMatrices(req.IdA, req.IdB);
        return new MultiplyResponseModel
        {
            Success = result.Success,
            ResultFileId = result.ResultFileId,
            Message = result.Message
        };
    }

    public InitDownloadResponseModel InitDownloadSoap(InitDownloadRequest req)
    {
        var logicResponse = _logic.InitDownload(req.FileId);
    
        return new InitDownloadResponseModel
        {
            Exists = logicResponse.Exists,
            TotalSizeBytes = logicResponse.TotalSizeBytes,
            ExpectedChunks = logicResponse.ExpectedChunks,
            ChunkSize = logicResponse.ChunkSize,
            Message = logicResponse.Message
        };
    }

    public DownloadChunkResponseModel DownloadChunkSoap(DownloadChunkRequest req)
    {
        var logicResponse = _logic.DownloadChunk(req.FileId, req.ChunkIndex);
        return new DownloadChunkResponseModel
        {
            Success = logicResponse.Success,
            Data = logicResponse.Data,
            IsComplete = logicResponse.IsComplete,
            Message = logicResponse.Message
        };
    }
}