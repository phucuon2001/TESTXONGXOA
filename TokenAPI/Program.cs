using Google.Apis.Auth.OAuth2;
using Google.Cloud.Logging.V2;
using Google.Protobuf.WellKnownTypes;
using System.Text.Json;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            int tokensUsedToday = await LogTokenFetcher.GetTodayTotalTokensAsync();
            Console.WriteLine($"🔍 Token đã dùng hôm nay: {tokensUsedToday}/300000 tokens");
        }
        catch (Exception ex)
        {
            Console.WriteLine("❌ Đã xảy ra lỗi:");
            Console.WriteLine(ex);
        }
    }
}

public class LogTokenFetcher
{
    private const string ServiceAccountPath = "service-account-file.json";
    public static async Task<int> GetTodayTotalTokensAsync()
    {
        var json = await File.ReadAllTextAsync(ServiceAccountPath);
        var jsonDoc = JsonDocument.Parse(json);
        string projectId = jsonDoc.RootElement.GetProperty("project_id").GetString();

        var credential = GoogleCredential.FromFile(ServiceAccountPath).CreateScoped("https://www.googleapis.com/auth/cloud-platform");

        var client = new LoggingServiceV2ClientBuilder
        {
            Credential = credential
        }.Build();

        string filter = $@"
            resource.type=""vertex_ai_endpoint""
            resource.labels.location=""us-central1""
            protoPayload.response.usageMetadata.totalTokenCount > 0
            timestamp >= ""{DateTime.UtcNow:yyyy-MM-dd}T00:00:00Z""";


        var request = new ListLogEntriesRequest
        {
            ResourceNames = { $"projects/{projectId}" },
            Filter = filter
        };

        int totalTokens = 0;

        var entries = client.ListLogEntries(request);
        foreach (var entry in entries)
        {
            var protoPayload = entry.ProtoPayload;
            if (protoPayload == null) continue;

            if (protoPayload.TryUnpack(out Struct payloadStruct))
            {
                if (payloadStruct.Fields.TryGetValue("response", out var responseField) &&
                    responseField.StructValue.Fields.TryGetValue("usageMetadata", out var usageField) &&
                    usageField.StructValue.Fields.TryGetValue("totalTokenCount", out var tokenField))
                {
                    totalTokens += (int)tokenField.NumberValue;
                }
            }
        }

        return totalTokens;
    }
}
