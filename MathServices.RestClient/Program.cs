using MathServices.RestClient;

double rowsPerChunk = 50;
int maxRetries = 3;
string serverUrl = "http://localhost:5156";
string baseDirectory = "/Users/norbertswistak/MathServicesSolution/MathServices.RestClient";


string matrixPath = Path.Combine(baseDirectory, "A2000.txt");

string name = Guid.NewGuid().ToString().Substring(0, 4) + "_Result.txt";
string outputPath = Path.Combine(baseDirectory, name);

string namef = Guid.NewGuid().ToString().Substring(0, 4) + "_Fractal.ppm";
string outputPathF = Path.Combine(baseDirectory, namef);



var (rows, cols, matrix) = MatrixFunc.ReadMatrixFromFile(matrixPath);
Console.WriteLine($"Plik wczytany. Wymiary: {rows}x{cols}");

using HttpClient client = new HttpClient();
client.BaseAddress = new Uri(serverUrl);


(string ? finalFileId,string msg)= await MatrixFunc.UploadMatrixAsync(client, matrix, rows, cols, rowsPerChunk, maxRetries);

if (finalFileId != null)
{
    Console.WriteLine($"\n{msg} Identyfikator wgranego pliku to: {finalFileId}");
}
else
{
    Console.WriteLine($"\n{msg} Plik nie zostal wgrany do konca.");
    return;
}

string? nextMatrixId = await MatrixFunc.MultiplyMatricesAsync(client, finalFileId ,finalFileId );
if (nextMatrixId != null)
{
    Console.WriteLine($"\nSUKCES! Identyfikator wyniku mnozenia to: {nextMatrixId}");
}
else
{
    Console.WriteLine("\nNIEPOWODZENIE: Mnozenie nie powiodlo sie.");
    return;
}

bool downloadSuccess = await MatrixFunc.DownloadMatrix(client, nextMatrixId, outputPath, maxRetries);
if (downloadSuccess)
{    
    Console.WriteLine($"\nWynik pobrany i zapisany do pliku '{Path.GetFileName(outputPath)}'.");
}
else
{    
    Console.WriteLine("\nNIEPOWODZENIE: Pobieranie wyniku nie  powiodlo sie.");
}   


// FRAKTAL


string? fractalId = await FractalFunc.GenerateFractalAsync(client, 20000, 20000, 10);

if (fractalId != null)
{
    
    await FractalFunc.DownloadFractalAsync(client, fractalId, outputPathF, maxRetries);
}
