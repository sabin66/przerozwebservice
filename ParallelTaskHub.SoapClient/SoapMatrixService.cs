using System.ServiceModel;

namespace ParallelTaskHub.SoapClient;

public static class SoapMatrixService
{
    public static (int rows, int cols, int[][] data) LoadMatrix(string path)
    {
        string[] lines = File.ReadAllLines(path);
        if (lines.Length == 0) throw new Exception("Plik jest pusty.");

        string[] header = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int rows = int.Parse(header[0]);
        int cols = int.Parse(header[1]);

        int[][] matrix = new int[rows][];
        for (int i = 0; i < rows; i++)
        {
            string[] rowData = lines[i + 1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            matrix[i] = new int[cols];
            for (int j = 0; j < cols; j++)
                matrix[i][j] = int.Parse(rowData[j]);
        }
        return (rows, cols, matrix);
    }

    public static ISoapMatrixEngine BuildSoapClient(string endpointUrl)
    {
        var binding = new BasicHttpBinding
        {
            MaxReceivedMessageSize = int.MaxValue,
            MaxBufferSize = int.MaxValue
        };
        var factory = new ChannelFactory<ISoapMatrixEngine>(binding, new EndpointAddress(endpointUrl));
        return factory.CreateChannel();
    }

    public static async Task<(string? fileId, string info)> UploadMatrixStreamAsync(
        ISoapMatrixEngine engine, int[][] matrix, int r, int c, double blockSize, int retries)
    {
        int chunks = (int)Math.Ceiling(r / blockSize);

        string sid = engine.InitUploadSoap(new MatrixInitRequest
        {
            TotalRows = r, TotalCols = c, ExpectedChunks = chunks
        });

        Console.WriteLine($"[SOAP] Zainicjowano sesję transferu: {sid}");

        string? uploadedId = null;
        string resultMsg = string.Empty;

        for (int i = 0; i < chunks; i++)
        {
            int from = i * (int)blockSize;
            int to = Math.Min(from + (int)blockSize, r);
            int len = to - from;

            int[][] block = new int[len][];
            Array.Copy(matrix, from, block, 0, len);

            // NAPRAWA: Zamiast int[][] wysyłamy płaską tablicę int[] + wymiary.
            // DataContractSerializer w .NET 10 preview nie obsługuje int[][] przez SOAP.
            int cols = block[0].Length;
            int[] flat = new int[len * cols];
            for (int row = 0; row < len; row++)
                Array.Copy(block[row], 0, flat, row * cols, cols);

            bool success = false;
            int attempt = 0;

            while (!success && attempt < retries)
            {
                attempt++;
                try
                {
                    Console.Write($" -> Wysyłanie paczki {i + 1}/{chunks} (próba {attempt})... ");
                    var res = engine.UploadChunkSoap(new MatrixChunkRequest
                    {
                        SessionId = sid,
                        ChunkIndex = i,
                        ChunkRows = len,
                        ChunkCols = cols,
                        RowsFlat = flat
                    });

                    if (res.Ok)
                    {
                        success = true;
                        Console.WriteLine("OK");
                        if (res.IsComplete) { uploadedId = res.FileId; resultMsg = res.Message; }
                    }
                    else
                    {
                        // Serwer odpowiedział Ok=false — logujemy Message żeby zdiagnozować przyczynę
                        Console.WriteLine($"[SERWER ODRZUCIŁ] ChunkIndex={res.ChunkIndex}, Msg={res.Message}");
                    }
                }
                catch (Exception ex) { Console.WriteLine($"Błąd połączenia : {ex.Message}"); }
                if (!success && attempt < retries) await Task.Delay(2000);
            }
            if (!success) return (null, "Przesyłanie przerwane.");
        }
        return (uploadedId, resultMsg);
    }

    public static string? RunMultiplication(ISoapMatrixEngine engine, string idA, string idB)
    {
        try
        {
            var res = engine.MultiplySoap(new MultiCalculationRequest { IdA = idA, IdB = idB });
            if (!res.Success) return null;
            Console.WriteLine($"[SOAP] Operacja mnożenia: {res.Message}");
            return res.ResultFileId;
        }
        catch { return null; }
    }

    public static async Task<bool> SaveMatrixFromServer(ISoapMatrixEngine engine, string id, string savePath, int retries)
    {
        var setup = engine.InitDownloadSoap(new DownloadSetupRequest { FileId = id });
        if (!setup.Exists) return false;

        int chunkCount = (int)Math.Ceiling(setup.TotalSizeBytes / (double)setup.ChunkSize);

        using (var fs = new FileStream(savePath, FileMode.Create))
        {
            for (int i = 0; i < chunkCount; i++)
            {
                bool ok = false;
                for (int a = 0; a < retries && !ok; a++)
                {
                    try
                    {
                        var data = engine.DownloadChunkSoap(new ChunkDataRequest { FileId = id, ChunkIndex = i });
                        if (data.Success)
                        {
                            await fs.WriteAsync(data.Data);
                            ok = true;
                            Console.Write($"\r[SOAP] Pobieranie wyniku: {((i + 1) * 100) / chunkCount}%");
                        }
                    }
                    catch { if (a < retries - 1) await Task.Delay(1500); }
                }
                if (!ok) return false;
            }
        }
        Console.WriteLine();
        return true;
    }
}