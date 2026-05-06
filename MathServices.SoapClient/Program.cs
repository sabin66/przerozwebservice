using MathServices.SoapClient;

Console.WriteLine("=== KLIENT SOAP ===");

double rowsPerChunk = 50;
int maxRetries = 3;
string baseDirectory = "/Users/norbertswistak/MathServicesSolution/MathServices.SoapClient";
string matrixPath = Path.Combine(baseDirectory, "A100.txt");
string name = Guid.NewGuid().ToString().Substring(0, 4) + "_Result_SOAP.txt";
string outputPath = Path.Combine(baseDirectory, name);

var soapClient = MatrixSoapFunc.CreateSoapClient("http://localhost:5156/MatrixSoap.svc");

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
