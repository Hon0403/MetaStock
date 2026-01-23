using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

// 1. 設定與讀取環境變數
var config = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .Build();

var supabaseKey = config["SUPABASE_KEY"];
var supabaseUrl = "https://ilpzjjzaxqdmvlvpiukc.supabase.co"; 

if (string.IsNullOrEmpty(supabaseKey))
{
    Console.WriteLine("Error: SUPABASE_KEY not found in environment variables.");
    Environment.Exit(1);
}

Console.WriteLine("Starting daily sync...");

using var httpClient = new HttpClient();

try
{
    // 2. 從證交所抓資料
    Console.WriteLine("Fetching data from TWSE...");
    var twseResponse = await httpClient.GetAsync("https://openapi.twse.com.tw/v1/exchangeReport/STOCK_DAY_ALL");
    twseResponse.EnsureSuccessStatusCode();

    var content = await twseResponse.Content.ReadAsStringAsync();
    using var doc = JsonDocument.Parse(content);
    var root = doc.RootElement;
    
    var dailyPrices = new List<object>();
    var stocksList = new List<object>();
    var today = DateTime.Today.ToString("yyyy-MM-dd");

    // 3. 解析與轉換資料
    foreach (var item in root.EnumerateArray())
    {
        var code = item.GetProperty("Code").GetString();
        var name = item.GetProperty("Name").GetString();
        
        if (code.Length > 4) continue; // 過濾權證

        // [NEW] 收集股票基本資料
        stocksList.Add(new {
            stock_id = code,
            name = name,
            market = "TSE" // 證交所 API 都是上市股票，先暫定 TSE
        });

        // 解析股價各欄位
        if (decimal.TryParse(item.GetProperty("OpeningPrice").GetString(), out var open) &&
            decimal.TryParse(item.GetProperty("HighestPrice").GetString(), out var high) &&
            decimal.TryParse(item.GetProperty("LowestPrice").GetString(), out var low) &&
            decimal.TryParse(item.GetProperty("ClosingPrice").GetString(), out var close) &&
            long.TryParse(item.GetProperty("TradeVolume").GetString(), out var volume) &&
            decimal.TryParse(item.GetProperty("Change").GetString(), out var change))
        {
            // 計算漲跌幅 %
            var changePercent = (close - change) != 0 ? Math.Round(change / (close - change) * 100, 2) : 0;

            dailyPrices.Add(new
            {
                stock_id = code,
                date = today,
                open = open,
                high = high,
                low = low,
                close = close,
                volume = volume,
                change = change,
                change_percent = changePercent
            });
        }
    }

    Console.WriteLine($"Prepared {stocksList.Count} stocks and {dailyPrices.Count} prices.");

    // 設定 Supabase Client Headers (共用)
    httpClient.DefaultRequestHeaders.Clear();
    httpClient.DefaultRequestHeaders.Add("apikey", supabaseKey);
    httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {supabaseKey}");
    httpClient.DefaultRequestHeaders.Add("Prefer", "resolution=merge-duplicates"); // UPSERT

    // 4. [NEW] 先寫入 Stocks (解決 Foreign Key 錯誤)
    Console.WriteLine("Syncing stocks info...");
    var stockBatches = stocksList.Chunk(1000); 
    foreach (var batch in stockBatches)
    {
        var json = JsonSerializer.Serialize(batch);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
        // 注意：這裡是用 POST 到 /stocks
        var response = await httpClient.PostAsync($"{supabaseUrl}/rest/v1/stocks", httpContent);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Failed to sync stocks batch: {error}");
        }
    }

    // 5. 再寫入 Daily Prices
    Console.WriteLine("Syncing daily prices...");
    var priceBatches = dailyPrices.Chunk(1000);
    foreach (var batch in priceBatches)
    {
        var json = JsonSerializer.Serialize(batch);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
        // 注意：這裡是用 POST 到 /daily_prices
        var response = await httpClient.PostAsync($"{supabaseUrl}/rest/v1/daily_prices", httpContent);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Failed to sync prices batch: {error}");
        }
    }

    Console.WriteLine("Sync process finished!");
}
catch (Exception ex)
{
    Console.WriteLine($"Critical Error: {ex.Message}");
    Environment.Exit(1);
}