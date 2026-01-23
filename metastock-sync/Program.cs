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
    // 本地測試請先執行: set SUPABASE_KEY=你的Key
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
    var today = DateTime.Today.ToString("yyyy-MM-dd");

    // 3. 解析與轉換資料
    foreach (var item in root.EnumerateArray())
    {
        var code = item.GetProperty("Code").GetString();
        if (code.Length > 4) continue; // 過濾權證

        // 解析各欄位
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

    Console.WriteLine($"Prepared {dailyPrices.Count} records.");

    // 4. 分批寫入 Supabase (每 1000 筆一次)
    var batchSize = 1000;
    var batches = dailyPrices.Chunk(batchSize);

    // 設定 Supabase Client Headers
    httpClient.DefaultRequestHeaders.Clear();
    httpClient.DefaultRequestHeaders.Add("apikey", supabaseKey);
    httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {supabaseKey}");
    httpClient.DefaultRequestHeaders.Add("Prefer", "resolution=merge-duplicates"); // UPSERT

    foreach (var batch in batches)
    {
        var json = JsonSerializer.Serialize(batch);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync($"{supabaseUrl}/rest/v1/daily_prices", httpContent);
        
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"Failed to sync batch: {error}");
            // Continue syncing other batches even if one fails
        }
    }

    Console.WriteLine("Sync process finished!");
}
catch (Exception ex)
{
    Console.WriteLine($"Critical Error: {ex.Message}");
    Environment.Exit(1);
}