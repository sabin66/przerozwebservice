using System.Collections.Concurrent;
namespace ParallelTaskHub.Server.Logic;

public class MandelbrotProcessor {
    private readonly string _dir = Path.Combine("Vault", "Fractals");
    private readonly int _buf = 65536;

    public MandelbrotProcessor() { if (!Directory.Exists(_dir)) Directory.CreateDirectory(_dir); }

    public string ComputeMandelbrot(int w, int h, int threads) {
        byte[] px = new byte[w * h * 3];
        var colors = new ConcurrentDictionary<int, byte[]>();
        var rng = new Random();
        Parallel.For(0, h, new ParallelOptions { MaxDegreeOfParallelism = threads }, y => {
            int tid = Environment.CurrentManagedThreadId;
            byte[] c = colors.GetOrAdd(tid, _ => { lock(rng) return new byte[]{(byte)rng.Next(100,255), (byte)rng.Next(100,255), (byte)rng.Next(100,255)}; });
            for (int x = 0; x < w; x++) {
                double zx=0, zy=0, cx=(x-w/2.0)*4.0/w, cy=(y-h/2.0)*4.0/w;
                int i=0; while(zx*zx+zy*zy<4 && i<255) { double t=zx*zx-zy*zy+cx; zy=2*zx*zy+cy; zx=t; i++; }
                int o=(y*w+x)*3; if(i<255){ float f=i/255f; px[o]=(byte)(c[0]*f); px[o+1]=(byte)(c[1]*f); px[o+2]=(byte)(c[2]*f); }
            }
        });
        string fn = $"img_{Guid.NewGuid():N}.ppm";
        using (var fs = new FileStream(Path.Combine(_dir, fn), FileMode.Create)) {
            fs.Write(System.Text.Encoding.ASCII.GetBytes($"P6\n{w} {h}\n255\n")); fs.Write(px);
        }
        return fn;
    }

    public DownloadSetupResponse SetupFractalDownload(string id) {
        string p = Path.Combine(_dir, id);
        if (!File.Exists(p)) return new DownloadSetupResponse { Exists = false };
        long sz = new FileInfo(p).Length;
        return new DownloadSetupResponse { Exists = true, TotalSizeBytes = sz, ExpectedChunks = (int)Math.Ceiling((double)sz/_buf), ChunkSize = _buf };
    }

    public ChunkDataResponse GetFractalPiece(string id, int idx) {
        string p = Path.Combine(_dir, id);
        using var fs = new FileStream(p, FileMode.Open, FileAccess.Read);
        long off = (long)idx * _buf; fs.Seek(off, SeekOrigin.Begin);
        byte[] b = new byte[Math.Min(_buf, fs.Length - off)]; fs.Read(b, 0, b.Length);
        return new ChunkDataResponse { Success = true, Data = b };
    }
}