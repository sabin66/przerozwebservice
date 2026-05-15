using System.ServiceModel;

namespace ParallelTaskHub.SoapClient;

public static class SoapMandelbrotService
{
    public static ISoapMandelbrotEngine BuildFractalClient(string url)
    {
        var binding = new BasicHttpBinding { MaxReceivedMessageSize = int.MaxValue, MaxBufferSize = int.MaxValue };
        var factory = new ChannelFactory<ISoapMandelbrotEngine>(binding, new EndpointAddress(url));
        return factory.CreateChannel();
    }

    public static string? TriggerGeneration(ISoapMandelbrotEngine engine, int w, int h, int threads)
    {
        try
        {
            Console.WriteLine($"\n[SOAP FRAKTAL] Zlecanie obliczeń ({w}x{h}, wątki: {threads})...");
            var res = engine.GenerateFractalSoap(new MandelbrotTaskRequest { Width = w, Height = h, maxThreads = threads });
            
            if (string.IsNullOrEmpty(res.FileId)) return null;

            Console.WriteLine($"[SOAP FRAKTAL] Obliczenia zakończone. ID: {res.FileId}");
            return res.FileId;
        }
        catch { return null; }
    }

    public static async Task<bool> DownloadImageAsync(ISoapMandelbrotEngine engine, string id, string path, int retries)
    {
        var setup = engine.InitDownloadFractalSoap(new DownloadSetupRequest { FileId = id });
        if (!setup.Exists) return false;
        
        Console.WriteLine($"[SOAP FRAKTAL] Pobieranie pliku (Bloków: {setup.ExpectedChunks})...");

        using (var stream = new FileStream(path, FileMode.Create))
        {
            for (int i = 0; i < setup.ExpectedChunks; i++)
            {
                bool success = false;
                for (int a = 0; a < retries && !success; a++)
                {
                    try
                    {
                        var chunk = engine.DownloadChunkFractalSoap(new ChunkDataRequest { FileId = id, ChunkIndex = i });
                        if (chunk.Success)
                        {
                            await stream.WriteAsync(chunk.Data);
                            success = true;
                            Console.Write($"\r[SOAP FRAKTAL] Postęp: {(i + 1) * 100 / setup.ExpectedChunks}%");
                        }
                    }
                    catch { if (a < retries - 1) await Task.Delay(2000); }
                }
                if (!success) return false;
            }
        }
        Console.WriteLine("\n[SOAP FRAKTAL] Plik obrazu pobrany pomyślnie.");
        return true;
    }
}