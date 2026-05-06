using MathServices.RestClient;

double rowsPerChunk = 50;
int maxRetries = 3;
string matrixPath = Path.Combine(AppContext.BaseDirectory, "A100.txt");
string serverUrl = "http://localhost:5156";

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
