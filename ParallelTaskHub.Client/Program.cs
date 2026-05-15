using ParallelTaskHub.Client;

int maxThreads = 8;
int fractalWidth = 1000;
int fractalHeight = 1000; 
double matrixBlockSize = 50; 
int networkRetries = 3;
string apiBase = "http://localhost:5156";

string workingDir = AppDomain.CurrentDomain.BaseDirectory;
string inputMatrix = Path.Combine(workingDir, "A100.txt");
string matrixResultFile = Path.Combine(workingDir, $"Mnozenie_{Guid.NewGuid().ToString().Substring(0,5)}.txt");
string fractalResultFile = Path.Combine(workingDir, $"Fraktal_{Guid.NewGuid().ToString().Substring(0,5)}.ppm");

Console.WriteLine("=== URUCHAMIANIE ===");

using HttpClient client = new HttpClient { BaseAddress = new Uri(apiBase) };

// Opóźnienie na start serwera
Console.WriteLine("Oczekiwanie na dostępność usług...");
await Task.Delay(6000);

try 
{
    // 1. MACIERZE
    var (r, c, data) = MatrixService.LoadMatrixData(inputMatrix);
    Console.WriteLine($"[MACIERZ] Dane wczytane: {r}x{c}");

    var (fileId, msg) = await MatrixService.TransferMatrixToServer(client, data, r, c, matrixBlockSize, networkRetries);
    if (fileId == null) throw new Exception("Nie udało się wysłać macierzy.");

    Console.WriteLine($"[MACIERZ] Serwer przyjął plik. ID: {fileId}");

    Console.WriteLine("[MACIERZ] Zlecanie mnożenia (A * A)...");
    string? resultId = await MatrixService.RequestMultiplication(client, fileId, fileId);
    
    if (resultId != null)
    {
        Console.WriteLine($"[MACIERZ] Mnożenie zakończone. Pobieranie wyniku {resultId}...");
        await MatrixService.FetchMatrixResult(client, resultId, matrixResultFile, networkRetries);
    }

    // 2. FRAKTAL
    Console.WriteLine($"\n[FRAKTAL] Generowanie Mandelbrota ({fractalWidth}x{fractalHeight}, Wątki: {maxThreads})...");
    string? fractalId = await MandelbrotService.StartFractalGeneration(client, fractalWidth, fractalHeight, maxThreads);

    if (fractalId != null)
    {
        await MandelbrotService.DownloadFractalImage(client, fractalId, fractalResultFile, networkRetries);
    }
}
catch (Exception ex)
{
    Console.WriteLine($"\n[BŁĄD KRYTYCZNY] {ex.Message}");
}

Console.WriteLine("\n=== KONIEC PRACY SYSTEMU ===");