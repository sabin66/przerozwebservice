using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MathServices.Logic;

public class FractalLogic
{
    private readonly string _storageDirectory = "FractalFiles";
    private readonly int _downloadChunkSize = 65536;

    private const double CxMin = -2.5;
    private const double CxMax = 1.5;
    private const double CyMin = -2.0;
    private const double CyMax = 2.0;
    private const int IterationMax = 200;
    private const double EscapeRadius = 2.0;

    public FractalLogic()
    {
        if (!Directory.Exists(_storageDirectory))
        {
            Directory.CreateDirectory(_storageDirectory);
        }
    }


    public string GenerateMandelbrot(int width, int height, int maxThreads)
    {
        if (width <= 0 || height <= 0)
            throw new ArgumentException("Wymiary obrazu muszą być większe od zera.");
        if (maxThreads <= 0)
            throw new ArgumentException("Liczba wątków musi być większa od zera.");

        byte[] imageData = new byte[width * height * 3];

        double pixelWidth = (CxMax - CxMin) / width;
        double pixelHeight = (CyMax - CyMin) / height;
        double er2 = EscapeRadius * EscapeRadius;

        ConcurrentDictionary<int, byte[]> threadColors = new ConcurrentDictionary<int, byte[]>();
        Random rand = new Random(); 

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxThreads
        };

        Parallel.For(0, height, parallelOptions, y =>
        {
            int threadId = Environment.CurrentManagedThreadId;

            byte[] threadColor = threadColors.GetOrAdd(threadId, _ =>
            {
                lock (rand) 
                {
                    return new byte[] { 
                        (byte)rand.Next(50, 255),
                        (byte)rand.Next(50, 255),
                        (byte)rand.Next(50, 255)
                    };
                }
            });

            double cy = CyMin + y * pixelHeight;
            if (Math.Abs(cy) < pixelHeight / 2) cy = 0.0;

            for (int x = 0; x < width; x++)
            {
                double cx = CxMin + x * pixelWidth;
                double zx = 0.0, zy = 0.0, zx2 = 0.0, zy2 = 0.0;
                int iteration;

                for (iteration = 0; iteration < IterationMax && (zx2 + zy2) < er2; iteration++)
                {
                    zy = 2 * zx * zy + cy;
                    zx = zx2 - zy2 + cx;
                    zx2 = zx * zx;
                    zy2 = zy * zy;
                }

                int pixelIndex = (y * width + x) * 3;

                if (iteration == IterationMax)
                {
                    imageData[pixelIndex] = 0;  
                    imageData[pixelIndex + 1] = 0; 
                    imageData[pixelIndex + 2] = 0; 
                }
                else
                {
                    double brightness = (double)iteration / IterationMax;
                    
                    imageData[pixelIndex] = (byte)(threadColor[0] * brightness);     
                    imageData[pixelIndex + 1] = (byte)(threadColor[1] * brightness); 
                    imageData[pixelIndex + 2] = (byte)(threadColor[2] * brightness); 
                }
            }
        });

        string id = $"fractal_{Guid.NewGuid()}.ppm";
        string filePath = Path.Combine(_storageDirectory, id);

        SavePpmFile(filePath, width, height, imageData);

        return id;
    }


    private void SavePpmFile(string filePath, int width, int height, byte[] imageData)
    {
        using (FileStream fs = new FileStream(filePath, FileMode.Create))
        using (BinaryWriter writer = new BinaryWriter(fs))
        {
            string header = $"P6\n# Fraktal wygenerowany w C# (TPL)\n{width} {height}\n255\n";
            writer.Write(System.Text.Encoding.ASCII.GetBytes(header));
            
            writer.Write(imageData);
        }
    }
    public InitDownloadResponse InitDownload(string fileId)
    {
        string filePath = Path.Combine(_storageDirectory, fileId);

        if (!File.Exists(filePath))
            return new InitDownloadResponse(false, 0, 0, _downloadChunkSize, $"Nie znaleziono fraktala o ID: {fileId}");

        long size = new FileInfo(filePath).Length;
        int chunks = (int)Math.Ceiling((double)size / _downloadChunkSize);

        return new InitDownloadResponse(true, size, chunks, _downloadChunkSize, "Fraktal gotowy do pobrania.");
    }

    public DownloadChunkResponse DownloadChunk(string fileId, int chunkIndex)
    {
        string filePath = Path.Combine(_storageDirectory, fileId);

        if (!File.Exists(filePath))
            return new DownloadChunkResponse(false, Array.Empty<byte>(), false, "Nie znaleziono pliku fraktala.");

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

            return new DownloadChunkResponse(true, buffer, (offset + bytesRead >= totalFileSize), "Pobrano paczkę fraktala.");
        }
    }
}