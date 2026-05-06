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
        Console.WriteLine($"\nUtworzono sesje: {sessionId}");

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
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<MultiplyResponse>();
            return result?.ResultFileId;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during multiplication: {ex.Message}");
            return null;
        }
    }
}








public record InitUploadResponse(string SessionId);
public record MultiplyResponse(string ResultFileId);
public record ChunkResponse(int ChunkIndex, bool Ok, bool IsComplete, string? FileId, string Message);

