using CoreWCF;
using CoreWCF.Configuration;
using CoreWCF.Description;
using System.Runtime.Serialization;
using ParallelTaskHub.Server;
using ParallelTaskHub.Server.Logic;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddServiceModelServices();
builder.Services.AddServiceModelMetadata();
builder.Services.AddSingleton<IServiceBehavior, UseRequestHeadersForMetadataAddressBehavior>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<MatrixEngine>();
builder.Services.AddSingleton<MandelbrotProcessor>();
builder.Services.AddSingleton<MatrixSoapGateway>();
builder.Services.AddSingleton<FractalSoapGateway>();

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// REST

app.MapPost("/api/matrix/upload/init", (MatrixInitRequest req, MatrixEngine engine) =>
    Results.Ok(new { SessionId = engine.PrepareUpload(req.TotalRows, req.TotalCols, req.ExpectedChunks) }));

app.MapPost("/api/matrix/upload/chunk", (MatrixChunkRequest req, MatrixEngine engine) => {
    var res = engine.ProcessChunk(req.SessionId, req.ChunkIndex, req.RowsChunk);
    return res.Ok ? Results.Ok(res) : Results.BadRequest(res);
});

app.MapPost("/api/matrix/multiply", (MultiCalculationRequest req, MatrixEngine engine) => {
    var res = engine.Multiply(req.IdA, req.IdB);
    return res.Success ? Results.Ok(res) : Results.BadRequest(res);
});

app.MapGet("/api/matrix/download/init/{id}", (string id, MatrixEngine engine) => {
    var res = engine.SetupDownload(id);
    return res.Exists ? Results.Ok(res) : Results.NotFound(res);
});

app.MapGet("/api/matrix/download/chunk/{id}/{idx}", (string id, int idx, MatrixEngine engine) => {
    var res = engine.GetPiece(id, idx);
    return res.Success ? Results.Ok(res) : Results.NotFound(res);
});

app.MapPost("/api/fractal/generate", (MandelbrotTaskRequest req, MandelbrotProcessor proc) =>
    Results.Ok(new { FileId = proc.ComputeMandelbrot(req.Width, req.Height, req.maxThreads) }));

app.MapGet("/api/fractal/download/init/{id}", (string id, MandelbrotProcessor proc) => {
    var res = proc.SetupFractalDownload(id);
    return res.Exists ? Results.Ok(res) : Results.NotFound(res);
});

app.MapGet("/api/fractal/download/chunk/{id}/{idx}", (string id, int idx, MandelbrotProcessor proc) => {
    var res = proc.GetFractalPiece(id, idx);
    return res.Success ? Results.Ok(res) : Results.NotFound(res);
});

// SOAP

app.UseServiceModel(sb =>
{
    var binding = new BasicHttpBinding {
        MaxReceivedMessageSize = int.MaxValue,
        MaxBufferSize = int.MaxValue
    };
    binding.ReaderQuotas.MaxArrayLength = int.MaxValue;
    binding.ReaderQuotas.MaxBytesPerRead = int.MaxValue;
    binding.ReaderQuotas.MaxStringContentLength = int.MaxValue;
    binding.ReaderQuotas.MaxDepth = 64;

    sb.AddService<MatrixSoapGateway>();
    sb.AddServiceEndpoint<MatrixSoapGateway, ISoapMatrixEngine>(binding, "/MatrixSoap.svc");

    sb.AddService<FractalSoapGateway>();
    sb.AddServiceEndpoint<FractalSoapGateway, ISoapMandelbrotEngine>(binding, "/FractalSoap.svc");

    app.Services.GetRequiredService<ServiceMetadataBehavior>().HttpGetEnabled = true;
});

app.Run();

// --- ŻĄDANIA (REQUEST) ---
public static class SoapConfig
{
    public const string Namespace = "http://schemas.datacontract.org/2004/07/";
}

[DataContract(Name = "MatrixInitRequest", Namespace = SoapConfig.Namespace)]
public class SoapMatrixInitRequest
{
    [DataMember] public int TotalRows { get; set; }
    [DataMember] public int TotalCols { get; set; }
    [DataMember] public int ExpectedChunks { get; set; }
}

[DataContract(Name = "MatrixChunkRequest", Namespace = SoapConfig.Namespace)]
public class SoapMatrixChunkRequest
{
    [DataMember] public string SessionId { get; set; } = string.Empty;
    [DataMember] public int ChunkIndex { get; set; }
    [DataMember] public int ChunkRows { get; set; }
    [DataMember] public int ChunkCols { get; set; }
    [DataMember] public int[] RowsFlat { get; set; } = Array.Empty<int>();
}

[DataContract(Name = "MultiCalculationRequest", Namespace = SoapConfig.Namespace)]
public class SoapMultiCalculationRequest
{
    [DataMember] public string IdA { get; set; } = string.Empty;
    [DataMember] public string IdB { get; set; } = string.Empty;
}

[DataContract(Name = "DownloadSetupRequest", Namespace = SoapConfig.Namespace)]
public class SoapDownloadSetupRequest
{
    [DataMember] public string FileId { get; set; } = string.Empty;
}

[DataContract(Name = "ChunkDataRequest", Namespace = SoapConfig.Namespace)]
public class SoapChunkDataRequest
{
    [DataMember] public string FileId { get; set; } = string.Empty;
    [DataMember] public int ChunkIndex { get; set; }
}

[DataContract(Name = "MandelbrotTaskRequest", Namespace = SoapConfig.Namespace)]
public class SoapMandelbrotTaskRequest
{
    [DataMember] public int Width { get; set; }
    [DataMember] public int Height { get; set; }
    [DataMember] public int maxThreads { get; set; }
}

// --- ODPOWIEDZI (RESPONSE) ---

[DataContract(Name = "SoapChunkResponse", Namespace = SoapConfig.Namespace)]
public class SoapChunkResponseDto
{
    [DataMember] public int ChunkIndex { get; set; }
    [DataMember] public bool Ok { get; set; }
    [DataMember] public bool IsComplete { get; set; }
    [DataMember] public string? FileId { get; set; }
    [DataMember] public string Message { get; set; } = string.Empty;
}

[DataContract(Name = "MultiCalcResponse", Namespace = SoapConfig.Namespace)]
public class SoapMultiCalcResponseDto
{
    [DataMember] public bool Success { get; set; }
    [DataMember] public string? ResultFileId { get; set; }
    [DataMember] public string Message { get; set; } = string.Empty;
}

[DataContract(Name = "DownloadSetupResponse", Namespace = SoapConfig.Namespace)]
public class SoapDownloadSetupResponseDto
{
    [DataMember] public bool Exists { get; set; }
    [DataMember] public long TotalSizeBytes { get; set; }
    [DataMember] public int ExpectedChunks { get; set; }
    [DataMember] public int ChunkSize { get; set; }
    [DataMember] public string Message { get; set; } = string.Empty;
}

[DataContract(Name = "ChunkDataResponse", Namespace = SoapConfig.Namespace)]
public class SoapChunkDataResponseDto
{
    [DataMember] public bool Success { get; set; }
    [DataMember] public byte[] Data { get; set; } = Array.Empty<byte>();
    [DataMember] public bool IsComplete { get; set; }
    [DataMember] public string Message { get; set; } = string.Empty;
}

[DataContract(Name = "MandelbrotTaskResponse", Namespace = SoapConfig.Namespace)]
public class SoapMandelbrotTaskResponseDto
{
    [DataMember] public string? FileId { get; set; }
    [DataMember] public string Message { get; set; } = string.Empty;
}

// =============================================================================
// INTERFEJSY I BRAMKI SOAP
// =============================================================================

[ServiceContract]
public interface ISoapMatrixEngine
{
    [OperationContract] string InitUploadSoap(SoapMatrixInitRequest request);
    [OperationContract] SoapChunkResponseDto UploadChunkSoap(SoapMatrixChunkRequest request);
    [OperationContract] SoapMultiCalcResponseDto MultiplySoap(SoapMultiCalculationRequest request);
    [OperationContract] SoapDownloadSetupResponseDto InitDownloadSoap(SoapDownloadSetupRequest request);
    [OperationContract] SoapChunkDataResponseDto DownloadChunkSoap(SoapChunkDataRequest request);
}

[ServiceBehavior(IncludeExceptionDetailInFaults = true)]
public class MatrixSoapGateway : ISoapMatrixEngine
{
    private readonly MatrixEngine _eng;
    public MatrixSoapGateway(MatrixEngine eng) => _eng = eng;

    public string InitUploadSoap(SoapMatrixInitRequest req) =>
        _eng.PrepareUpload(req.TotalRows, req.TotalCols, req.ExpectedChunks);

    public SoapChunkResponseDto UploadChunkSoap(SoapMatrixChunkRequest req)
    {
        int rows = req.ChunkRows, cols = req.ChunkCols;
        int[][] jagged = new int[rows][];
        for (int i = 0; i < rows; i++)
        {
            jagged[i] = new int[cols];
            Array.Copy(req.RowsFlat, i * cols, jagged[i], 0, cols);
        }
        var r = _eng.ProcessChunk(req.SessionId, req.ChunkIndex, jagged);
        return new SoapChunkResponseDto { ChunkIndex = r.ChunkIndex, Ok = r.Ok, IsComplete = r.IsComplete, FileId = r.FileId, Message = r.Message };
    }

    public SoapMultiCalcResponseDto MultiplySoap(SoapMultiCalculationRequest req)
    {
        var r = _eng.Multiply(req.IdA, req.IdB);
        return new SoapMultiCalcResponseDto { Success = r.Success, ResultFileId = r.ResultFileId, Message = r.Message };
    }

    public SoapDownloadSetupResponseDto InitDownloadSoap(SoapDownloadSetupRequest req)
    {
        var r = _eng.SetupDownload(req.FileId);
        return new SoapDownloadSetupResponseDto { Exists = r.Exists, TotalSizeBytes = r.TotalSizeBytes, ExpectedChunks = r.ExpectedChunks, ChunkSize = r.ChunkSize, Message = r.Exists? "OK" : "BRAK PLIKU" };
    }

    public SoapChunkDataResponseDto DownloadChunkSoap(SoapChunkDataRequest req)
    {
        var r = _eng.GetPiece(req.FileId, req.ChunkIndex);
        return new SoapChunkDataResponseDto { Success = r.Success, Data = r.Data, IsComplete = r.IsComplete, Message = r.Success? "OK" : "BRAK PLIKU"  };
    }
}

[ServiceContract]
public interface ISoapMandelbrotEngine
{
    [OperationContract] SoapMandelbrotTaskResponseDto GenerateFractalSoap(SoapMandelbrotTaskRequest request);
    [OperationContract] SoapDownloadSetupResponseDto InitDownloadFractalSoap(SoapDownloadSetupRequest request);
    [OperationContract] SoapChunkDataResponseDto DownloadChunkFractalSoap(SoapChunkDataRequest request);
}

[ServiceBehavior(IncludeExceptionDetailInFaults = true)]
public class FractalSoapGateway : ISoapMandelbrotEngine
{
    private readonly MandelbrotProcessor _proc;
    public FractalSoapGateway(MandelbrotProcessor proc) => _proc = proc;

    public SoapMandelbrotTaskResponseDto GenerateFractalSoap(SoapMandelbrotTaskRequest req) =>
        new SoapMandelbrotTaskResponseDto { FileId = _proc.ComputeMandelbrot(req.Width, req.Height, req.maxThreads), Message = "Sukces" };

    public SoapDownloadSetupResponseDto InitDownloadFractalSoap(SoapDownloadSetupRequest req)
    {
        var r = _proc.SetupFractalDownload(req.FileId);
        return new SoapDownloadSetupResponseDto { Exists = r.Exists, TotalSizeBytes = r.TotalSizeBytes, ExpectedChunks = r.ExpectedChunks, ChunkSize = r.ChunkSize, Message = r.Exists? "OK" : "BRAK PLIKU"  };
    }

    public SoapChunkDataResponseDto DownloadChunkFractalSoap(SoapChunkDataRequest req)
    {
        var r = _proc.GetFractalPiece(req.FileId, req.ChunkIndex);
        return new SoapChunkDataResponseDto { Success = r.Success, Data = r.Data, IsComplete = r.IsComplete, Message = r.Success? "OK" : "BRAK PLIKU"  };
    }
}