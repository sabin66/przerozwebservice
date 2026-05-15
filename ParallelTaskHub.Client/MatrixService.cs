using System.Net.Http.Json;

namespace ParallelTaskHub.Client;

public static class MatrixService
{
    // Zmieniona nazwa z ReadMatrixFromFile na LoadMatrixData
    public static (int rows, int cols, int[][] data) LoadMatrixData(string filePath)
    {
        string[] content = File.ReadAllLines(filePath);
        if (content.Length == 0) throw new Exception("Błąd: Wskazany plik jest pusty.");

        string[] info = content[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int r = int.Parse(info[0]);
        int c = int.Parse(info[1]);

        if (content.Length - 1 < r) throw new Exception("Błąd: Niekompletne dane w pliku.");

        int[][] matrix = new int[r][];
        for (int i = 0; i < r; i++)
        {
            string[] rowValues = content[i + 1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            matrix[i] = new int[c];
            for (int j = 0; j < c; j++)
                matrix[i][j] = int.Parse(rowValues[j]);
        }
        return (r, c, matrix);
    }

    // Zmieniona nazwa z UploadMatrixAsync na TransferMatrixToServer
    public static async Task<(string? id, string note)> TransferMatrixToServer(HttpClient http, int[][] matrix, int r, int c, double blockSize, int retries)
    {
        int totalParts = (int)Math.Ceiling(r / blockSize);

        var initReq = await http.PostAsJsonAsync("/api/matrix/upload/init", 
            new { TotalRows = r, TotalCols = c, ExpectedChunks = totalParts });

        initReq.EnsureSuccessStatusCode();
        var initInfo = await initReq.Content.ReadFromJsonAsync<InitUploadResponse>();
        string sid = initInfo!.SessionId;
        
        Console.WriteLine($"[INFO] Sesja przesyłania rozpoczęta: {sid}");

        string? finalId = null;
        string logMsg = string.Empty;

        for (int i = 0; i < totalParts; i++)
        {
            int start = i * (int)blockSize;
            int end = Math.Min(start + (int)blockSize, r);
            int count = end - start;

            int[][] part = new int[count][];
            Array.Copy(matrix, start, part, 0, count);

            bool isSent = false;
            int attempt = 0;

            while (!isSent && attempt < retries)
            {
                attempt++;
                try
                {
                    Console.Write($" -> Wysyłanie bloku {i + 1}/{totalParts} (próba {attempt})... ");
                    var response = await http.PostAsJsonAsync("/api/matrix/upload/chunk", new { SessionId = sid, ChunkIndex = i, RowsChunk = part });
                    response.EnsureSuccessStatusCode();

                    var result = await response.Content.ReadFromJsonAsync<ChunkResponse>();
                    if (result != null && result.Ok)
                    {
                        isSent = true;
                        Console.WriteLine("Sukces");
                        if (result.IsComplete) { finalId = result.FileId; logMsg = result.Message; }
                    }
                }
                catch { Console.WriteLine("Ponawianie..."); }
                if (!isSent && attempt < retries) await Task.Delay(1500);
            }
            if (!isSent) return (null, "Krytyczny błąd podczas transferu.");
        }
        return (finalId, logMsg);
    }

    public static async Task<string?> RequestMultiplication(HttpClient http, string aId, string bId)
    {
        var response = await http.PostAsJsonAsync("/api/matrix/multiply", new { IdA = aId, IdB = bId });
        if (!response.IsSuccessStatusCode) return null;

        var res = await response.Content.ReadFromJsonAsync<MultiplyResponse>();
        return res?.Success == true ? res.ResultFileId : null;
    }

    public static async Task<bool> FetchMatrixResult(HttpClient http, string fileId, string path, int retries)
    {
        var init = await http.GetAsync($"/api/matrix/download/init/{fileId}");
        if (!init.IsSuccessStatusCode) return false;

        var data = await init.Content.ReadFromJsonAsync<InitDownloadResponse>();
        if (data == null || !data.Exists) return false;

        using var stream = new FileStream(path, FileMode.Create);
        for (int i = 0; i < data.ExpectedChunks; i++)
        {
            bool ok = false;
            for (int a = 0; a < retries && !ok; a++)
            {
                var chunk = await http.GetAsync($"/api/matrix/download/chunk/{fileId}/{i}");
                if (chunk.IsSuccessStatusCode)
                {
                    var cData = await chunk.Content.ReadFromJsonAsync<DownloadChunkResponse>();
                    if (cData != null && cData.Success)
                    {
                        await stream.WriteAsync(cData.Data);
                        ok = true;
                        Console.Write($"\r[POBIERANIE] Postęp: {(i + 1) * 100 / data.ExpectedChunks}%");
                    }
                }
                if (!ok) await Task.Delay(1000);
            }
            if (!ok) return false;
        }
        Console.WriteLine();
        return true;
    }
}