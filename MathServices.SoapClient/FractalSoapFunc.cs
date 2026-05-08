using System.ServiceModel;

namespace MathServices.SoapClient;

public static class FractalSoapFunc
{
    public static IFractalSoapService CreateFractalSoapClient(string url = "http://localhost:5156/FractalSoap.svc")
    {
        var binding = new BasicHttpBinding();
        binding.MaxReceivedMessageSize = int.MaxValue; 
        binding.MaxBufferSize = int.MaxValue;

        var endpoint = new EndpointAddress(url);
        var factory = new ChannelFactory<IFractalSoapService>(binding, endpoint);
        return factory.CreateChannel();
    }

    public static string? GenerateFractal(IFractalSoapService soapClient, int width, int height, int maxThreads)
    {
        try
        {
            Console.WriteLine($"\n[SOAP FRACTAL] Zlecanie generowania ({width}x{height}, wątki: {maxThreads})...");
            var request = new FractalRequest { Width = width, Height = height, maxThreads = maxThreads };
            
            var result = soapClient.GenerateFractalSoap(request);
            
            if (string.IsNullOrEmpty(result.FileId))
            {
                Console.WriteLine($"[SOAP FRACTAL] Błąd: {result.Message}");
                return null;
            }

            Console.WriteLine($"[SOAP FRACTAL] Sukces! ID pliku: {result.FileId}");
            return result.FileId;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SOAP FRACTAL] Błąd komunikacji: {ex.Message}");
            return null;
        }
    }

    public static async Task<bool> DownloadFractalAsync(IFractalSoapService soapClient, string fileId, string outputFilePath, int maxRetries)
    {
        try
        {
            var initData = soapClient.InitDownloadFractalSoap(new InitDownloadRequest { FileId = fileId });
            
            if (!initData.Exists)
            {
                Console.WriteLine($"\n[SOAP FRACTAL] Serwer odrzucił żądanie: {initData.Message}");
                return false;
            }
            
            int expectedChunks = initData.ExpectedChunks;
            Console.WriteLine($"[SOAP FRACTAL] Rozpoczęto pobieranie (Paczek: {expectedChunks})...");

            using (FileStream fs = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write))
            {
                for (int i = 0; i < expectedChunks; i++)
                {
                    bool success = false;
                    int attempt = 0;

                    while (!success && attempt < maxRetries)
                    {
                        attempt++;
                        try
                        {
                            var chunkData = soapClient.DownloadChunkFractalSoap(new DownloadChunkRequest { FileId = fileId, ChunkIndex = i });
                            
                            if (chunkData.Success && chunkData.Data != null)
                            {
                                await fs.WriteAsync(chunkData.Data, 0, chunkData.Data.Length);
                                success = true;
                                int percentComplete = ((i + 1) * 100) / expectedChunks;
                                Console.Write($"\rPostęp: {percentComplete}%");
                            }
                            else
                            {
                                Console.WriteLine($"\nFailed chunk {i}: {chunkData.Message}");
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"\nError chunk {i}: {e.Message}");
                            if (attempt < maxRetries) await Task.Delay(TimeSpan.FromSeconds(2));
                        }
                    }
                    if (!success) return false;
                }
            }
            Console.WriteLine("\n[SOAP FRACTAL] Obraz zapisany pomyślnie na dysku!");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError during fractal download: {ex.Message}");
            return false;
        }
    }
}