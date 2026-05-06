using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace MathServices.Logic;

public record ChunkResponse(int ChunkIndex, bool Ok, bool IsComplete, string? FileId, string Message);
public record DownloadChunkResponse(bool Success, byte[] Data, bool IsComplete, string Message);
public record InitDownloadResponse(bool Exists, long TotalSizeBytes, int ExpectedChunks, int ChunkSize, string Message);
public record MultiplyLogicResponse(bool Success, string? ResultFileId, string Message);

public class UploadSession
{
    public string SessionId { get; set; } = string.Empty;
    public int ExpectedChunks { get; set; }
    public int NextExpectedChunkIndex { get; set; } = 0;
    public string FilePath { get; set; } = string.Empty;
    public object WriteLock { get; } = new object();
}

public class MatrixLogic
{
    private readonly string _storageDirectory = "MatrixFiles";
    
    private readonly int _downloadChunkSize = 65536; 
    
    private static readonly ConcurrentDictionary<string, UploadSession> _activeUploads = new();

    public MatrixLogic()
    {
        if (!Directory.Exists(_storageDirectory)) Directory.CreateDirectory(_storageDirectory);
    }
    
    public string InitUpload(int totalRows, int totalCols, int expectedChunks)
    {
        string sessionId = Guid.NewGuid().ToString(); 
        string tempFilePath = Path.Combine(_storageDirectory, sessionId + ".tmp");

        var session = new UploadSession { SessionId = sessionId, ExpectedChunks = expectedChunks, FilePath = tempFilePath };
        _activeUploads.TryAdd(sessionId, session);

        using (StreamWriter writer = new StreamWriter(tempFilePath, append: false))
        {
            writer.WriteLine($"{totalRows} {totalCols}");
        }
        return sessionId;
    }

    public ChunkResponse UploadChunk(string sessionId, int chunkIndex, int[][] rowsChunk)
    {
        if (!_activeUploads.TryGetValue(sessionId, out var session))
            return new ChunkResponse(chunkIndex, false, false, null, "Nie znaleziono aktywnej sesji przesyłania o podanym ID.");

        lock (session.WriteLock)
        {
            if (chunkIndex != session.NextExpectedChunkIndex)
                return new ChunkResponse(chunkIndex, false, false, null, $"Błąd kolejności! Oczekiwano {session.NextExpectedChunkIndex}, otrzymano {chunkIndex}.");            

            using (StreamWriter writer = new StreamWriter(session.FilePath, append: true))
            {
                for (int i = 0; i < rowsChunk.Length; i++)
                {
                    StringBuilder sb = new StringBuilder();
                    for (int j = 0; j < rowsChunk[i].Length; j++)
                    {
                        sb.Append(rowsChunk[i][j]);
                        if (j < rowsChunk[i].Length - 1) sb.Append(' ');
                    }
                    writer.WriteLine(sb.ToString());
                }
            }

            session.NextExpectedChunkIndex++;
            bool isComplete = (session.NextExpectedChunkIndex == session.ExpectedChunks);
            string? finalFileId = null; 

            if (isComplete)
            {
                finalFileId = Guid.NewGuid().ToString() + ".txt";
                string finalFilePath = Path.Combine(_storageDirectory, finalFileId);
                File.Move(session.FilePath, finalFilePath);
                _activeUploads.TryRemove(sessionId, out _);
            }

            return new ChunkResponse(chunkIndex, true, isComplete, finalFileId, isComplete ? "Przesyłanie zakończone!" : $"Paczka {chunkIndex} przyjęta.");
        }
    }

    public int[][] GetMatrix(string id)
    {
        string filePath = Path.Combine(_storageDirectory, id);
        if (!File.Exists(filePath)) throw new FileNotFoundException($"Nie znaleziono macierzy: {id}");

        using (StreamReader reader = new StreamReader(filePath))
        {
            string? header = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(header)) throw new InvalidDataException("Plik jest pusty.");

            string[] dims = header.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int rows = int.Parse(dims[0]);
            int cols = int.Parse(dims[1]);

            int[][] matrix = new int[rows][];
            for (int i = 0; i < rows; i++)
            {
                matrix[i] = new int[cols];
                string? line = reader.ReadLine();
                if (line != null)
                {
                    string[] values = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    for (int j = 0; j < cols; j++) matrix[i][j] = int.Parse(values[j]);
                }
            }
            return matrix;
        }
    }

    public MultiplyLogicResponse MultiplyMatrices(string idA, string idB)
    {
        try
        {
            int[][] A = GetMatrix(idA);
            int[][] B = GetMatrix(idB);

            int rowsA = A.Length, colsA = A[0].Length;
            int rowsB = B.Length, colsB = B[0].Length;

            if (colsA != rowsB)
                return new MultiplyLogicResponse(false, null, "Niezgodne wymiary macierzy.");

            int[][] C = new int[rowsA][];
            for (int i = 0; i < rowsA; i++) C[i] = new int[colsB];

            Parallel.For(0, rowsA, i =>
            {
                for (int j = 0; j < colsB; j++)
                {
                    int sum = 0;
                    for (int k = 0; k < colsA; k++) sum += A[i][k] * B[k][j];
                    C[i][j] = sum;
                }
            });

            string newFileId = SaveResultMatrix(C);
            return new MultiplyLogicResponse(true, newFileId, "Mnożenie zakończone sukcesem.");
        }
        catch (Exception ex)
        {
            return new MultiplyLogicResponse(false, null, $"Błąd mnożenia: {ex.Message}");
        }
    }

    private string SaveResultMatrix(int[][] matrix)
    {
        int rows = matrix.Length, cols = matrix[0].Length;
        string id = Guid.NewGuid().ToString() + ".txt";
        string filePath = Path.Combine(_storageDirectory, id);

        using (StreamWriter writer = new StreamWriter(filePath))
        {
            writer.WriteLine($"{rows} {cols}");
            for (int i = 0; i < rows; i++)
            {
                StringBuilder sb = new StringBuilder();
                for (int j = 0; j < cols; j++)
                {
                    sb.Append(matrix[i][j]);
                    if (j < cols - 1) sb.Append(' ');
                }
                writer.WriteLine(sb.ToString());
            }
        }
        return id;
    }

    public InitDownloadResponse InitDownload(string fileId)
    {
        string filePath = Path.Combine(_storageDirectory, fileId);

        if (!File.Exists(filePath))
            return new InitDownloadResponse(false, 0, 0, _downloadChunkSize, $"Nie znaleziono pliku o ID: {fileId}");
        long size = new FileInfo(filePath).Length;
        int chunks = (int)Math.Ceiling((double)size / _downloadChunkSize);

        return new InitDownloadResponse(true, size, chunks, _downloadChunkSize, "Plik gotowy do pobrania.");
    }

    public DownloadChunkResponse DownloadChunk(string fileId, int chunkIndex)
    {
        string filePath = Path.Combine(_storageDirectory, fileId);

        if (!File.Exists(filePath))
            return new DownloadChunkResponse(false, Array.Empty<byte>(), false, "Nie znaleziono pliku paczki.");

        long totalFileSize = new FileInfo(filePath).Length;

        long offset = (long)chunkIndex * _downloadChunkSize;

        if (offset >= totalFileSize)
            return new DownloadChunkResponse(true, Array.Empty<byte>(), true, "Koniec pliku.");

        using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            stream.Seek(offset, SeekOrigin.Begin);
            long remainingBytes = totalFileSize - offset;
            int bytesToRead = (int)Math.Min(_downloadChunkSize, remainingBytes);

            byte[] buffer = new byte[bytesToRead];
            int bytesRead = stream.Read(buffer, 0, bytesToRead);

            return new DownloadChunkResponse(true, buffer, (offset + bytesRead >= totalFileSize), "Pobrano paczkę.");
        }
    }
}