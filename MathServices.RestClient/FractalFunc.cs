using System.Net.Http.Json;

namespace MathServices.RestClient;

public static class FractalFunc
{
    public static async Task<string?> GenerateFractalAsync(HttpClient client, int width, int height, int maxThreads)
    {
        try
        {
            var response = await client.PostAsJsonAsync("/api/fractal/generate", new 
            { 
                Width = width, 
                Height = height, 
                maxThreads = maxThreads 
            });

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadFromJsonAsync<ErrorMessageResponse>();
                Console.WriteLine($"Blad podczas generowania: {error?.Message ?? response.ReasonPhrase}");
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<FractalGenerateResponse>();
            if (result != null && !string.IsNullOrEmpty(result.FileId))
            {
                Console.WriteLine($"Przypisane ID pliku: {result.FileId}");
                return result.FileId;
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Krytyczny blad podczas zlecania generowania: {ex.Message}");
            return null;
        }
    }

    public static async Task<bool> DownloadFractalAsync(HttpClient client, string fileId, string outputFilePath, int maxRetries)
    {
        try
        {
            var initResponse = await client.GetAsync($"/api/fractal/download/init/{fileId}");
            if (!initResponse.IsSuccessStatusCode)
            {
                var error = await initResponse.Content.ReadFromJsonAsync<ErrorMessageResponse>();
                Console.WriteLine($"\nSerwer odrzucil: {error?.Message ?? initResponse.ReasonPhrase}");
                return false;
            }

            var initData = await initResponse.Content.ReadFromJsonAsync<InitDownloadResponse>();

            if (initData == null || !initData.Exists)
            {
                Console.WriteLine($"\nSerwer odrzucił żądanie pobierania: {initData?.Message}");
                return false;
            }

            int expectedChunks = initData.ExpectedChunks;
            Console.WriteLine($"Rozpoczęto pobieranie fraktala {fileId} (Paczek: {expectedChunks})...");

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
                            var chunkResponse = await client.GetAsync($"/api/fractal/download/chunk/{fileId}/{i}");
                            if (!chunkResponse.IsSuccessStatusCode)
                            {
                                var error = await chunkResponse.Content.ReadFromJsonAsync<ErrorMessageResponse>();
                                Console.WriteLine($"Failed to download chunk {i}. {error?.Message ?? chunkResponse.ReasonPhrase}");
                                break;
                            }

                            var chunkData = await chunkResponse.Content.ReadFromJsonAsync<DownloadChunkResponse>();
                            if (chunkData != null && chunkData.Success && chunkData.Data != null)
                            {
                                await fs.WriteAsync(chunkData.Data, 0, chunkData.Data.Length);
                                success = true;
                                int percentComplete = ((i + 1) * 100) / expectedChunks;
                                Console.Write($"\rPostęp: {percentComplete}%");
                            }
                            else
                            {
                                Console.WriteLine($"\nFailed to download chunk {i}. {chunkData?.Message ?? "Nieznany błąd."}");
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"\nError downloading chunk {i} (attempt {attempt}): {e.Message}");
                            if (attempt < maxRetries) await Task.Delay(TimeSpan.FromSeconds(2));
                        }
                    }

                    if (!success)
                    {
                        Console.WriteLine($"\nFailed to download chunk {i} after {maxRetries} attempts.");
                        return false;
                    }
                }
            }
            Console.WriteLine();
            Console.WriteLine("[FRACTAL] Zapisano plik pomyślnie na dysku!");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError during download: {ex.Message}");
            return false;
        }
    }
}

public record FractalGenerateResponse(string FileId);