using System.Net.Http.Json;

namespace MathServices.RestClient;

public static class MatrixFunc
{
    public static (int rows, int cols, int[][] matrix) ReadMatrixFromFile(string path)
    {
        string[] lines = File.ReadAllLines(path);
        if (lines.Length == 0) throw new InvalidDataException("Plik pusty.");

        string[] header = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int rows = int.Parse(header[0]);
        int cols = int.Parse(header[1]);

        if (lines.Length - 1 < rows)
            throw new InvalidDataException("Za malo wierszy z danymi.");

        int[][] matrix = new int[rows][];
        for (int i = 0; i < rows; i++)
        {
            string[] parts = lines[i + 1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != cols)
                throw new InvalidDataException($"Wiersz {i} ma zla liczbe kolumn.");

            matrix[i] = new int[cols];
            for (int j = 0; j < cols; j++)
                matrix[i][j] = int.Parse(parts[j]);
        }

        return (rows, cols, matrix);
    }

    public static async Task<(string? fileId, string message)> UploadMatrixAsync(HttpClient client, int[][] matrix, int rows, int cols, double rowsPerChunk, int maxRetries)
    {
        int expectedChunks = (int)Math.Ceiling(rows / rowsPerChunk);

        var id = await client.PostAsJsonAsync("/api/matrix/upload/init",
            new { TotalRows = rows, TotalCols = cols, ExpectedChunks = expectedChunks });

        id.EnsureSuccessStatusCode();

        var initResult = await id.Content.ReadFromJsonAsync<InitUploadResponse>();
        string sessionId = initResult!.SessionId;
        Console.WriteLine($"\nSession create: {sessionId}");

        string? uploadedFileId = null;
        string message = String.Empty;

        for (int i = 0; i < expectedChunks; i++)
        {
            int startRow = i * (int)rowsPerChunk;
            int endRow = Math.Min(startRow + (int)rowsPerChunk, rows);
            int currentAmount = endRow - startRow;

            int[][] chunk = new int[currentAmount][];
            for (int j = 0; j < currentAmount; j++)
            {
                chunk[j] = matrix[startRow + j];
            }

            bool success = false;
            int attempt = 0;

            while (!success && attempt < maxRetries)
            {
                attempt++;
                try
                {
                    Console.Write($"Uploading chunk {i + 1}/{expectedChunks} (attempt {attempt})... ");
                    var chunkResponse = await client.PostAsJsonAsync("/api/matrix/upload/chunk", new
                    {
                        SessionId = sessionId,
                        ChunkIndex = i,
                        RowsChunk = chunk
                    });

                    chunkResponse.EnsureSuccessStatusCode();

                    var chunkResult = await chunkResponse.Content.ReadFromJsonAsync<ChunkResponse>();

                    if (chunkResult != null && chunkResult.Ok)
                    {
                        success = true;
                        Console.WriteLine("OK");
                        if (chunkResult.IsComplete)
                        {
                            uploadedFileId = chunkResult.FileId;
                            message = chunkResult.Message;
                        }
                    }
                    else
                    {   
                        Console.WriteLine($"Failed. Response: {chunkResponse.StatusCode}");
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error: {e.Message}");
                }

                if (!success && attempt < maxRetries)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2));
                }
            }

            if (!success)
            {
                Console.WriteLine($"KRYTYCZNY BLAD: Upload failed on attempt {attempt}");
                return (null, message);
            }
        }

        return (uploadedFileId, message);
    }
    public static async Task<string?> MultiplyMatricesAsync(HttpClient client, string idA, string idB)
    {
        try
        {
            var response = await client.PostAsJsonAsync("/api/matrix/multiply", new { IdA = idA, IdB = idB });
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadFromJsonAsync<ErrorMessageResponse>();
                Console.WriteLine($"Error during multiplication: {error?.Message ?? response.ReasonPhrase}");
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<MultiplyResponse>();
            if (result == null || !result.Success)
            {
                Console.WriteLine($"Error during multiplication: {result?.Message ?? "Nieznany błąd."}");
                return null;
            }

            return result.ResultFileId;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during multiplication: {ex.Message}");
            return null;
        }
    }

    public static async Task<bool> DownloadMatrix(HttpClient client, string fileId, string outputFilePath,
        int maxRetries)
    {
        try
        {
            var initResponse = await client.GetAsync($"/api/matrix/download/init/{fileId}");
            if (!initResponse.IsSuccessStatusCode)
            {
                var error = await initResponse.Content.ReadFromJsonAsync<ErrorMessageResponse>();
                Console.WriteLine($"\nSerwer odrzucił żądanie: {error?.Message ?? initResponse.ReasonPhrase}");
                return false;
            }

            var initData = await initResponse.Content.ReadFromJsonAsync<InitDownloadResponse>();

            if (initData == null || !initData.Exists)
            {
                Console.WriteLine($"\nSerwer odrzucił żądanie: {initData?.Message ?? "Nieznany błąd."}");
                return false;
            }

            int expectedChunks = (int)Math.Ceiling(initData.TotalSizeBytes / (double)initData.ChunkSize);

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
                            var chunkResponse = await client.GetAsync($"/api/matrix/download/chunk/{fileId}/{i}");
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
                                Console.WriteLine("OK");
                            }
                            else
                            {
                                Console.WriteLine($"Failed to download chunk {i}. {chunkData?.Message ?? "Nieznany błąd."}");
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"Error downloading chunk {i} (attempt {attempt}): {e.Message}");
                            if (attempt < maxRetries) await Task.Delay(TimeSpan.FromSeconds(2));
                        }
                    }

                    if (!success)
                    {
                        Console.WriteLine($"Failed to download chunk {i} after {maxRetries} attempts.");
                        return false;
                    }
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during download: {ex.Message}");
            return false;
        }
    }    
}







public record ErrorMessageResponse(string Message);
public record InitUploadResponse(string SessionId);
public record MultiplyResponse(bool Success, string? ResultFileId, string Message);
public record ChunkResponse(int ChunkIndex, bool Ok, bool IsComplete, string? FileId, string Message);
public record InitDownloadResponse(bool Exists, long TotalSizeBytes, int ExpectedChunks, int ChunkSize, string Message);
public record DownloadChunkResponse(bool Success, byte[] Data, bool IsComplete, string Message);
