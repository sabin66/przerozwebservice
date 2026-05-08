using MathServices.SoapClient;

Console.WriteLine("=== KLIENT SOAP ===");

double rowsPerChunk = 50;
int maxRetries = 3;
string baseDirectory = "/Users/norbertswistak/MathServicesSolution/MathServices.SoapClient";
string baseDirectoryF = "/Users/norbertswistak/MathServicesSolution/MathServices.SoapClient";
string matrixPath = Path.Combine(baseDirectory, "A100.txt");
string name = Guid.NewGuid().ToString().Substring(0, 4) + "_Result_SOAP.txt";
string nameF = Guid.NewGuid().ToString().Substring(0, 4) + "_Fractal_SOAP.ppm";

string outputPath = Path.Combine(baseDirectory, name);
string outputPathF = Path.Combine(baseDirectory, nameF);


var soapClient = MatrixSoapFunc.CreateSoapClient("http://localhost:5156/MatrixSoap.svc");
var fractalSoapClient = FractalSoapFunc.CreateFractalSoapClient("http://localhost:5156/FractalSoap.svc");

Console.WriteLine("\nWczytywanie pliku...");
var (rows, cols, matrix) = MatrixSoapFunc.ReadMatrixFromFile(matrixPath);
Console.WriteLine($"Plik wczytany. Wymiary: {rows}x{cols}");

var (fileIdA, msgA) = await MatrixSoapFunc.UploadMatrixAsync(soapClient, matrix, rows, cols, rowsPerChunk, maxRetries);
var fileIdB = fileIdA;
if (fileIdA == null || fileIdB == null) return;
Console.WriteLine($"\nWgrano macierze: \nA = {fileIdA} \nB = {fileIdB}");

string? resultId = MatrixSoapFunc.MultiplyMatrices(soapClient, fileIdA, fileIdB);

if (resultId != null)
{
    Console.WriteLine($"\nPobieranie wyniku do: {outputPath}...");
    
    bool downloaded = await MatrixSoapFunc.DownloadMatrixAsync(soapClient, resultId, outputPath, maxRetries);
    
    if (downloaded)
    {
        Console.WriteLine("\n[SUKCES] Cały proces SOAP zakończony pomyślnie!");
    }
}

string? fractalId = FractalSoapFunc.GenerateFractal(fractalSoapClient, 20000, 20000, 4);

if (fractalId != null)
{
    Console.WriteLine($"\nPobieranie wyniku do: {outputPathF}...");
    
    bool downloaded = await FractalSoapFunc.DownloadFractalAsync(fractalSoapClient, fractalId, outputPathF, maxRetries);
    
    if (downloaded)
    {
        Console.WriteLine("\n[SUKCES] Cały proces SOAP dla fraktali zakończony pomyślnie!");
    }
}
