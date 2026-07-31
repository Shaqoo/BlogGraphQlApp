using Pinecone.Grpc;
using System.Text;
using System.Text.Json;

namespace BlogGraphQlApp.External
{
    public class PineconeService
    {
        private readonly HttpClient _http;
        private readonly string? _apiKey;
        private readonly string? _environment;
        private readonly string? _indexName;
        private readonly string? _host;

        public PineconeService(HttpClient http, IConfiguration config)
        {
            _http = http;
            _apiKey = config["Pinecone:ApiKey"];
            _environment = config["Pinecone:Environment"];
            _indexName = config["Pinecone:IndexName"];
            _host = config["Pinecone:Host"];
        }

        public async Task UpsertAsync(string id, float[] vector, object metadata)
        {
            var payload = new
            {
                vectors = new[]
                {
                    new {
                        id,
                        values = vector,
                        metadata
                    }
                }
            };
            var url = $"{_host}/vectors/upsert";
            using (var request = new HttpRequestMessage(HttpMethod.Post,url)
            {
                Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json")
            }) 
            {
                request.Headers.Add("Api-Key", _apiKey);
                var response = await _http.SendAsync(request);
                response.EnsureSuccessStatusCode();
            }

        }

        public async Task<IEnumerable<string>> QueryAsync(float[] vector, int topK = 10)
        {
            var payload = new
            {
                vector,
                topK,
                includeMetadata = true
            };

            var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_host}/query")
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json")
            };

            request.Headers.Add("Api-Key", _apiKey);

            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            return doc.RootElement
                .GetProperty("matches")
                .EnumerateArray()
                .Select(x => x.GetProperty("id").GetString()!)
                .ToList();
        }

    }
}
