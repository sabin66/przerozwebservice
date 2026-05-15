using System.Collections.Concurrent;
namespace ParallelTaskHub.Server.Logic;

public class ActiveSession {
    public string Id { get; set; } = string.Empty;
    public int TotalParts { get; set; }
    public int CurrentIndex { get; set; } = 0;
    public string Path { get; set; } = string.Empty;
    public object Lock { get; } = new object();
}

public class MatrixEngine {
    private readonly string _baseDir = Path.Combine("Vault", "Matrices");
    private readonly int _chunkSize = 65536; 
    private static readonly ConcurrentDictionary<string, ActiveSession> _sessions = new();

    public MatrixEngine() { if (!Directory.Exists(_baseDir)) Directory.CreateDirectory(_baseDir); }
    
    public string PrepareUpload(int r, int c, int parts) {
        string sid = Guid.NewGuid().ToString("N"); 
        string tmpPath = Path.Combine(_baseDir, sid + ".upload");
        _sessions.TryAdd(sid, new ActiveSession { Id = sid, TotalParts = parts, Path = tmpPath });
        File.WriteAllText(tmpPath, $"{r} {c}\n");
        return sid;
    }

    public SoapChunkResponse ProcessChunk(string sid, int idx, int[][] data) {
        if (!_sessions.TryGetValue(sid, out var session)) return new SoapChunkResponse { Ok = false };
        lock (session.Lock) {
            using (var sw = new StreamWriter(session.Path, true)) {
                foreach (var row in data) sw.WriteLine(string.Join(" ", row));
            }
            session.CurrentIndex++;
            bool last = (session.CurrentIndex == session.TotalParts);
            string? fid = null;
            if (last) {
                fid = $"matrix_{Guid.NewGuid():N}.txt";
                File.Move(session.Path, Path.Combine(_baseDir, fid));
                _sessions.TryRemove(sid, out _);
            }
            return new SoapChunkResponse { ChunkIndex = idx, Ok = true, IsComplete = last, FileId = fid, Message = last ? "OK" : "Part OK" };
        }
    }

    public MultiCalcResponse Multiply(string id1, string id2) {
        try {
            int[][] A = Load(id1); int[][] B = Load(id2);
            int rows = A.Length, cols = B[0].Length, common = B.Length;
            int[][] C = new int[rows][];
            Parallel.For(0, rows, i => {
                C[i] = new int[cols];
                for (int j = 0; j < cols; j++) {
                    int v = 0; for (int k = 0; k < common; k++) v += A[i][k] * B[k][j];
                    C[i][j] = v;
                }
            });
            return new MultiCalcResponse { Success = true, ResultFileId = Save(C), Message = "Mnożenie OK" };
        } catch (Exception ex) { return new MultiCalcResponse { Success = false, Message = ex.Message }; }
    }

    private int[][] Load(string id) {
        var lines = File.ReadAllLines(Path.Combine(_baseDir, id));
        var d = lines[0].Split(' ');
        return lines.Skip(1).Select(l => l.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray()).ToArray();
    }

    private string Save(int[][] m) {
        string id = $"res_{Guid.NewGuid():N}.txt";
        using var sw = new StreamWriter(Path.Combine(_baseDir, id));
        sw.WriteLine($"{m.Length} {m[0].Length}");
        foreach (var r in m) sw.WriteLine(string.Join(" ", r));
        return id;
    }

    public DownloadSetupResponse SetupDownload(string id) {
        string p = Path.Combine(_baseDir, id);
        if (!File.Exists(p)) return new DownloadSetupResponse { Exists = false };
        long sz = new FileInfo(p).Length;
        return new DownloadSetupResponse { Exists = true, TotalSizeBytes = sz, ExpectedChunks = (int)Math.Ceiling((double)sz / _chunkSize), ChunkSize = _chunkSize };
    }

    public ChunkDataResponse GetPiece(string id, int idx) {
        string p = Path.Combine(_baseDir, id);
        using var fs = new FileStream(p, FileMode.Open, FileAccess.Read);
        long off = (long)idx * _chunkSize;
        fs.Seek(off, SeekOrigin.Begin);
        byte[] b = new byte[Math.Min(_chunkSize, fs.Length - off)];
        fs.Read(b, 0, b.Length);
        return new ChunkDataResponse { Success = true, Data = b, IsComplete = (off + b.Length >= fs.Length) };
    }
}