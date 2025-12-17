/* 
* Public domain software, no restrictions. 
* Released by rickd3ckard: https://github.com/rickd3ckard/ 
* See: https://unlicense.org/
*/

using System.Collections.Specialized;
using System.Web;
using System.Text.Json;
using MySql.Data.MySqlClient;
using System.Text;
using System.Text.Encodings.Web;

class Program
{
    static List<Uri> _validUris = new List<Uri>();

    public static async Task Main(string[] args)
    {
        Options options = ValidateArguments(args);

        using (HttpClient client = new HttpClient())
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.Accept.ParseAdd("image/webp,*/*;q=0.8"); ;
            client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
            client.DefaultRequestHeaders.Referrer = new Uri("https://github.com/rickd3ckard/");

            int pageIndex = 0;
            while (true)
            {
                pageIndex += 1;
                string rawText = await FetchRawPage(client, pageIndex, options.Filter);
                List<Entry>? entries = ConvertToEntryList(rawText);
                if (entries == null) { throw new ArgumentNullException(nameof(entries) + "is null."); }
                await ProcessEntryList(client, entries);

                if (options.SqlInformationComplete)
                {
                    PushToDatabase(_validUris, options.SqlServer, options.SqlUserName, options.SqlPassword, options.SqlDatabase);
                }

                JsonSerializerOptions jsonOptions = new JsonSerializerOptions();
                jsonOptions.WriteIndented = true;
                jsonOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                jsonOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;

                string outputFileName = string.IsNullOrWhiteSpace(options.OutputFilePath) ? "output.txt" : options.OutputFilePath;
                if (File.Exists(outputFileName))
                {
                    List<Uri>? existingList = JsonSerializer.Deserialize<List<Uri>>(File.ReadAllText(outputFileName));
                    if (existingList == null) { throw new InvalidCastException(); }
                    _validUris.AddRange(existingList);
                }

                File.WriteAllText(outputFileName, JsonSerializer.Serialize(_validUris, jsonOptions));
            }
        }
    }

    static Options ValidateArguments(string[] args)
    {
        if (args.Length % 2 != 0) { throw new InvalidOperationException("Invalid arguments count."); }
        Options options = new Options();

        for (int i = 0; i <= args.Length - 1; i += 2)
        {
            switch (args[i])
            {
                case "-s":
                    if (!string.IsNullOrWhiteSpace(args[i + 1])) { options.SqlServer = args[i + 1]; break; }
                    else { throw new InvalidOperationException("Argument value can not be null or white space."); };
                case "-u":
                    if (!string.IsNullOrWhiteSpace(args[i + 1])) { options.SqlUserName = args[i + 1]; break; }
                    else { throw new InvalidOperationException("Argument value can not be null or white space."); };
                case "-p":
                    if (!string.IsNullOrWhiteSpace(args[i + 1])) { options.SqlPassword = args[i + 1]; break; }
                    else { throw new InvalidOperationException("Argument value can not be null or white space."); };
                case "-d":
                    if (!string.IsNullOrWhiteSpace(args[i + 1])) { options.SqlDatabase = args[i + 1]; break; }
                    else { throw new InvalidOperationException("Argument value can not be null or white space."); };
                case "-o":
                    if (!string.IsNullOrWhiteSpace(args[i + 1])) { options.OutputFilePath = args[i + 1]; break; }
                    else { throw new InvalidOperationException("Argument value can not be null or white space."); };
                case "-f":
                    if (!string.IsNullOrWhiteSpace(args[i + 1])) { options.Filter = args[i + 1]; break; }
                    else { throw new InvalidOperationException("Argument value can not be null or white space."); };
                default: throw new InvalidOperationException("Invalid argument: " + args[i]);
            }
        }

        bool allSqlNullOrWhiteSpace = string.IsNullOrWhiteSpace(options.SqlServer) && string.IsNullOrWhiteSpace(options.SqlUserName) && string.IsNullOrWhiteSpace(options.SqlPassword) && string.IsNullOrWhiteSpace(options.SqlDatabase);
        bool noneSqlNullOrWhiteSpace = !string.IsNullOrWhiteSpace(options.SqlServer) && !string.IsNullOrWhiteSpace(options.SqlUserName) && !string.IsNullOrWhiteSpace(options.SqlPassword) && !string.IsNullOrWhiteSpace(options.SqlDatabase);
        if (!(allSqlNullOrWhiteSpace || noneSqlNullOrWhiteSpace)) { throw new InvalidOperationException("Some arguments are missing for SQL database."); }
        if (noneSqlNullOrWhiteSpace) { options.SqlInformationComplete = true; }

        if (string.IsNullOrWhiteSpace(options.Filter)) { throw new InvalidOperationException("Domain filter can not be null or white space. Example: .be"); }
        Console.WriteLine(options); return options;
    }

    static async Task<string> FetchRawPage(HttpClient Client, int PageIndex, string Filter)
    {
        while (true) // keep retrying 
        {
            try
            {
                // example https://crt.sh:443/?q=%25.be&output=json&page=1
                UriBuilder uriBuilder = new UriBuilder(@"https://crt.sh/");
                NameValueCollection query = HttpUtility.ParseQueryString(string.Empty);
                query["q"] = $"%{Filter}";
                query["output"] = "json";
                query["page"] = PageIndex.ToString();
                uriBuilder.Query = query.ToString();

                HttpResponseMessage response = await Client.GetAsync(uriBuilder.Uri);
                response.EnsureSuccessStatusCode();
                string rawText = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrWhiteSpace(rawText)) { throw new InvalidOperationException(nameof(rawText)); }
                return rawText;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex); Console.ResetColor(); await Task.Delay(5000); // avoid rate limit
            }
        }
    }

    static List<Entry>? ConvertToEntryList(string RawText)
    {
        try
        {
            List<Entry>? entries = JsonSerializer.Deserialize<List<Entry>>(RawText) ?? null;
            if (entries == null) { throw new InvalidOperationException(); }
            return entries;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(ex); Console.ResetColor(); return null;
        }
    }

    static async Task ProcessEntryList(HttpClient Client, List<Entry> Entries)
    {
        _validUris.Clear();
        Queue<Uri> uriPool = new Queue<Uri>();
        foreach (Entry entry in Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.common_name)) { continue; }
            if (!entry.common_name.EndsWith(".be")) { continue; }

            string cleanHost = entry.common_name;
            if (cleanHost.Count(c => c == '.') > 1)
            {
                int lastIndex = cleanHost.LastIndexOf('.');
                cleanHost = cleanHost.Substring(cleanHost.LastIndexOf('.', lastIndex - 1) + 1);
            }

            UriBuilder builder = new UriBuilder();
            builder.Scheme = "https";
            builder.Host = cleanHost;
            uriPool.Enqueue(builder.Uri);
        }

        List<Task> taskPool = new List<Task>();
        for (byte i = 0; i < 50 && uriPool.Count > 0; i++)
        {
            Uri uri = uriPool.Dequeue();
            taskPool.Add(CheckUri(Client, uri));
        }

        while (taskPool.Count > 0)
        {
            Task finishedTask = await Task.WhenAny(taskPool);
            taskPool.Remove(finishedTask);

            if (uriPool.Count > 0)
            {
                Uri nextUri = uriPool.Dequeue();
                taskPool.Add(CheckUri(Client, nextUri));
            }
        }
    }

    static async Task CheckUri(HttpClient Client, Uri Uri)
    {
        try
        {
            CancellationTokenSource timeoutToken = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            HttpResponseMessage response2 = await Client.GetAsync(Uri, timeoutToken.Token);
            response2.EnsureSuccessStatusCode();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(Uri + " -> " + response2.StatusCode); Console.ResetColor();
            _validUris.Add(Uri);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(Uri + " -> " + ex.Message); Console.ResetColor();
        }
    }

    static void PushToDatabase(List<Uri> UriList, string SqlServer, string SqlUserName, string SqlPassword, string SqlDatabase)
    {
        string connectionString = $"server={SqlServer};uid={SqlUserName};pwd={SqlPassword};database={SqlDatabase};Convert Zero Datetime=True";
        using (MySqlConnection conn = new MySqlConnection(connectionString))
        {
            conn.Open();
            StringBuilder command = new StringBuilder();
            command.Append("INSERT INTO domains VALUES ");
            for (int i = 0; i < UriList.Count; i++)
            {
                command.Append($"(@domain{i})");
                if (i == UriList.Count - 1) { command.Append(";"); }
                else { command.Append(","); }
            }
            MySqlCommand cmd = new MySqlCommand(command.ToString(), conn);
            for (int i = 0; i < UriList.Count; i++) { cmd.Parameters.AddWithValue($"@domain{i}", UriList[i]); }
            cmd.ExecuteNonQuery();
        }
    }
}

public class Entry
{
    public Entry() { }

    public int? issuer_ca_id { get; set; }
    public string? issuer_name { get; set; }
    public string? common_name { get; set; }
    public string? name_value { get; set; }
    public int? id { get; set; }
    public string? entry_timestamp { get; set; }
    public string? not_before { get; set; }
    public string? not_after { get; set; }
    public string? serial_number { get; set; }
    public int? result_count { get; set; }

    public override string ToString()
    {
        JsonSerializerOptions options = new JsonSerializerOptions();
        options.WriteIndented = true;
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;

        return JsonSerializer.Serialize(this, options);
    }
}

public class Options
{
    public Options() { }

    public string SqlServer { get; set; } = string.Empty;
    public string SqlUserName { get; set; } = string.Empty;
    public string SqlPassword { get; set; } = string.Empty;
    public string SqlDatabase { get; set; } = string.Empty;
    public string OutputFilePath { get; set; } = string.Empty;
    public string Filter { get; set; } = string.Empty;
    public bool SqlInformationComplete { get; set; } = false;

    public override string ToString()
    {
        JsonSerializerOptions options = new JsonSerializerOptions();
        options.WriteIndented = true;
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;

        return JsonSerializer.Serialize(this, options);
    }
}