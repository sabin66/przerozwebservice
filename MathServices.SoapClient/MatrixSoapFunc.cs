using System.ServiceModel;

namespace MathServices.SoapClient;

public static class MatrixSoapFunc
{
    public static (int rows, int cols, int[][] matrix) ReadMatrixFromFile(string path)
    {
        string[] lines = File.ReadAllLines(path);
        if (lines.Length == 0) throw new InvalidDataException("Plik pusty.");

        string[] header = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int rows = int.Parse(header[0]);
        int cols = int.Parse(header[1]);

        int[][] matrix = new int[rows][];
        for (int i = 0; i < rows; i++)
        {
            string[] parts = lines[i + 1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            matrix[i] = new int[cols];
            for (int j = 0; j < cols; j++)
                matrix[i][j] = int.Parse(parts[j]);
        }
        return (rows, cols, matrix);
    }
    
    ///<summary>
    /// ZAPYTAC CZY MOZNA ZWIEKSZYC BUFFER 
    ///</summary>
    public static IMatrixSoapService CreateSoapClient(string url = "http://localhost:5156/MatrixSoap.svc")
    {
        var binding = new BasicHttpBinding();

        binding.MaxReceivedMessageSize = int.MaxValue; 
        binding.MaxBufferSize = int.MaxValue;

        var endpoint = new EndpointAddress(url);
        var factory = new ChannelFactory<IMatrixSoapService>(binding, endpoint);
        return factory.CreateChannel();
    }

    public static async Task<(string? fileId, string message)> UploadMatrixAsync(IMatrixSoapService soapClient, int[][] matrix, int rows, int cols, double rowsPerChunk, int maxRetries)
    {
        int expectedChunks = (int)Math.Ceiling(rows / rowsPerChunk);

        string sessionId = soapClient.InitUploadSoap(new InitUploadRequest 
        { 
            TotalRows = rows, TotalCols = cols, ExpectedChunks = expectedChunks 
        });
        
        Console.WriteLine($"\n[SOAP] Utworzono sesje: {sessionId}");

        string? uploadedFileId = null;
        string message = string.Empty;

        for (int i = 0; i < expectedChunks; i++)
        {
            int startRow = i * (int)rowsPerChunk;
            int endRow = Math.Min(startRow + (int)rowsPerChunk, rows);
            int currentAmount = endRow - startRow;

            int[][] chunk = new int[currentAmount][];
            for (int j = 0; j < currentAmount; j++) chunk[j] = matrix[startRow + j];

            bool success = false;
            int attempt = 0;

            while (!success && attempt < maxRetries)
            {
                attempt++;
                try
                {
                    Console.Write($"Uploading chunk {i + 1}/{expectedChunks} (attempt {attempt})... ");
                    
                    var chunkResult = soapClient.UploadChunkSoap(new UploadChunkRequest 
                    { 
                        SessionId = sessionId, ChunkIndex = i, RowsChunk = chunk 
                    });

                    if (chunkResult.Ok)
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
                        Console.WriteLine($"Failed. Z serwera: {chunkResult.Message}");
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error: {e.Message}");
                }

                if (!success && attempt < maxRetries) await Task.Delay(TimeSpan.FromSeconds(2));
            }

            if (!success) return (null, message);
        }

        return (uploadedFileId, message);
    }

    public static string? MultiplyMatrices(IMatrixSoapService soapClient, string idA, string idB)
    {
        try
        {
            var result = soapClient.MultiplySoap(new MultiplyRequest { IdA = idA, IdB = idB });
            
            if (!result.Success)
            {
                Console.WriteLine($"\n[SOAP] Błąd mnożenia: {result.Message}");
                return null;
            }

            Console.WriteLine($"\n[SOAP] Mnożenie sukces: {result.Message}");
            return result.ResultFileId;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n[SOAP] Krytyczny błąd: {ex.Message}");
            return null;
        }
    }

    public static async Task<bool> DownloadMatrixAsync(IMatrixSoapService soapClient, string fileId, string outputFilePath, int maxRetries)
    {
        try
        {
            var initData = soapClient.InitDownloadSoap(new InitDownloadRequest { FileId = fileId });
            
            if (!initData.Exists)
            {
                Console.WriteLine($"\n[SOAP] Serwer odrzucił żądanie: {initData.Message}");
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
                            var chunkData = soapClient.DownloadChunkSoap(new DownloadChunkRequest { FileId = fileId, ChunkIndex = i });
                            
                            if (chunkData.Success && chunkData.Data != null)
                            {
                                await fs.WriteAsync(chunkData.Data, 0, chunkData.Data.Length);
                                success = true;
                                int percentComplete = ((i + 1) * 100) / expectedChunks;
                                Console.Write($"\rPostęp: {percentComplete}%");
                            }
                            else
                            {
                                Console.WriteLine($"Failed chunk {i}: {chunkData.Message}");
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"Error chunk {i}: {e.Message}");
                            if (attempt < maxRetries) await Task.Delay(TimeSpan.FromSeconds(2));
                        }
                    }
                    if (!success) return false;
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