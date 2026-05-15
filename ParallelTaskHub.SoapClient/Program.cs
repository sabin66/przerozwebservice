using ParallelTaskHub.SoapClient;

// --- PARAMETRY KONFIGURACYJNE ---
int threadCount = 4;        // Liczba wątków do generowania fraktala
int imgWidth = 2000;        // Szerokość fraktala
int imgHeight = 2000;       // Wysokość fraktala
double chunkSize = 50;      // Liczba wierszy macierzy w jednej paczce
int retryLimit = 3;         // Maksymalna liczba powtórzeń przy błędzie
string baseServiceUrl = "http://localhost:5156";

string currentPath = AppDomain.CurrentDomain.BaseDirectory;
string matrixSource = Path.Combine(currentPath, "A100.txt");
string matrixResult = Path.Combine(currentPath, $"SOAP_Matrix_{Guid.NewGuid().ToString().Substring(0, 4)}.txt");
string fractalResult = Path.Combine(currentPath, $"SOAP_Fractal_{Guid.NewGuid().ToString().Substring(0, 4)}.ppm");

Console.WriteLine("=== PARALLEL TASK HUB - KLIENT SOAP ===");

var matrixClient = SoapMatrixService.BuildSoapClient($"{baseServiceUrl}/MatrixSoap.svc");
var fractalClient = SoapMandelbrotService.BuildFractalClient($"{baseServiceUrl}/FractalSoap.svc");

Console.WriteLine("Oczekiwanie na start serwera SOAP...");
await Task.Delay(5000);

try
{
    // 1. OBSŁUGA MACIERZY
    Console.WriteLine("\nWczytywanie danych wejściowych...");
    var (r, c, matrixData) = SoapMatrixService.LoadMatrix(matrixSource);
    Console.WriteLine($"Wczytano macierz: {r}x{c}");

    var (fileIdA, msgA) = await SoapMatrixService.UploadMatrixStreamAsync(matrixClient, matrixData, r, c, chunkSize, retryLimit);
    if (fileIdA == null) return;

    Console.WriteLine($"\n[MACIERZ] Przesłano dane pod ID: {fileIdA}");

    Console.WriteLine("[MACIERZ] Uruchamianie mnożenia macierzy (A * A)...");
    string? resultId = SoapMatrixService.RunMultiplication(matrixClient, fileIdA, fileIdA);

    if (resultId != null)
    {
        Console.WriteLine($"[MACIERZ] Pobieranie wyniku mnożenia do pliku...");
        await SoapMatrixService.SaveMatrixFromServer(matrixClient, resultId, matrixResult, retryLimit);
    }

    // 2. OBSŁUGA FRAKTALA
    Console.WriteLine($"\n[FRAKTAL] Zlecanie generowania Mandelbrota (Wątki: {threadCount})...");
    string? fractalId = SoapMandelbrotService.TriggerGeneration(fractalClient, imgWidth, imgHeight, threadCount);

    if (fractalId != null)
    {
        await SoapMandelbrotService.DownloadImageAsync(fractalClient, fractalId, fractalResult, retryLimit);
    }

    Console.WriteLine("\n[SUKCES] Cały proces SOAP przebiegł pomyślnie!");
}
catch (Exception ex)
{
    Console.WriteLine($"\n[BŁĄD KRYTYCZNY] {ex.Message}");
}