using CsvHelper;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
    
class Program
{
    static async Task Main(string[] args)
    {
        var ativo = "XAUUSD";

        var httpClient = new HttpClient();
        var apiUrl = "http://localhost:5053/orders/next"; // Ajuste a URL conforme necessário

        var registros = await GerarJsonAsync();

        foreach (var registro in registros.OrderBy(x=> x.Data))
        {
            var url = $"{apiUrl}?Symbol={ativo}&Bid={registro.Maxima}&Ask={registro.Minima}&Time={registro.Data}";
            var response = await httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            Console.WriteLine($"[{registro.Data}] {content}");
            await Task.Delay(100); // Simula intervalo entre ticks (ajuste conforme necessário)
        }
    }

    static async Task<List<PrecoHistorico>> GerarJsonAsync()
    {
        var csvPath = @"c:\users\dougl\downloads\XAUUSD_05-06-09-2025.csv";
        var jsonPath = "XAUUSD.json";

        using var reader = new StreamReader(csvPath, Encoding.UTF8);
        var config = new CsvHelper.Configuration.CsvConfiguration(new CultureInfo("pt-BR"))
        {
            Delimiter = ",", // or ";" if that's what your file uses
        };
        using var csv = new CsvReader(reader, config); /* CultureInfo.InvariantCulture);*/

        var registros = new List<PrecoHistorico>();
        await foreach (var registro in csv.GetRecordsAsync<PrecoHistorico>())
        {
            registros.Add(registro);
        }

        //var options = new JsonSerializerOptions { WriteIndented = true };
        //var json = JsonSerializer.Serialize(registros, options);
        //await File.WriteAllTextAsync(jsonPath, json);

        //Console.WriteLine($"Arquivo {jsonPath} gerado com sucesso!");

        return registros;
    }
}

