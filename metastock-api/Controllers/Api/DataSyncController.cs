using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text;

namespace metastock_api.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class DataSyncController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public DataSyncController(IConfiguration configuration, IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _httpClient = httpClientFactory.CreateClient();
        }

        [HttpPost("sync-daily-prices")]
        public async Task<IActionResult> SyncDailyPrices()
        {
            try
            {
                // 1. 取得 Supabase 設定
                var supabaseUrl = "https://ilpzjjzaxqdmvlvpiukc.supabase.co";
                var supabaseKey = _configuration["Supabase:Key"];

                if (string.IsNullOrEmpty(supabaseKey))
                {
                    return BadRequest("Supabase Key not configured");
                }

                // 2. 從證交所取得每日收盤行情 (全部股票)
                // API: https://openapi.twse.com.tw/v1/exchangeReport/STOCK_DAY_ALL
                var twseResponse = await _httpClient.GetAsync("https://openapi.twse.com.tw/v1/exchangeReport/STOCK_DAY_ALL");
                twseResponse.EnsureSuccessStatusCode();
                
                var content = await twseResponse.Content.ReadAsStringAsync();
                var twseData = JsonSerializer.Deserialize<List<TwseStockDayData>>(content);

                if (twseData == null || !twseData.Any())
                {
                    return BadRequest("No data fetched from TWSE");
                }

                // 3. 資料轉換 (TWSE -> Supabase Format)
                var dailyPrices = new List<object>();
                var today = DateTime.Today.ToString("yyyy-MM-dd"); // 使用今日日期，或從 API 資料中解析日期如果有的話 (STOCK_DAY_ALL 通常是當日)

                foreach (var item in twseData)
                {
                    // 過濾掉非股票 (代號長度 > 4 或是權證等) - 這裡先簡單過濾長度
                    if (item.Code.Length > 4) continue;

                    if (decimal.TryParse(item.OpeningPrice, out var open) &&
                        decimal.TryParse(item.HighestPrice, out var high) &&
                        decimal.TryParse(item.LowestPrice, out var low) &&
                        decimal.TryParse(item.ClosingPrice, out var close) &&
                        long.TryParse(item.TradeVolume, out var volume) &&
                        decimal.TryParse(item.Change, out var change))
                    {
                        dailyPrices.Add(new
                        {
                            stock_id = item.Code,
                            date = today, // 注意: STOCK_DAY_ALL 沒有回傳日期欄位，通常是「當下交易日」的資料。如果要精確，需另行處理。
                            open = open,
                            high = high,
                            low = low,
                            close = close,
                            volume = volume,
                            change = change,
                            // change_percent 需自行計算: change / (close - change) * 100
                            change_percent = (close - change) != 0 ? Math.Round(change / (close - change) * 100, 2) : 0
                        });
                    }
                }

                // 4. 分批寫入 Supabase (避免一次 Request 太大)
                var batchSize = 1000;
                var batches = dailyPrices.Chunk(batchSize);
                var client = new HttpClient();
                client.DefaultRequestHeaders.Add("apikey", supabaseKey);
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {supabaseKey}");
                // Prefer: resolution=merge-duplicates 會執行 upsert (如果 PK 衝突則更新)
                client.DefaultRequestHeaders.Add("Prefer", "resolution=merge-duplicates");

                foreach (var batch in batches)
                {
                    var json = JsonSerializer.Serialize(batch);
                    var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
                    var response = await client.PostAsync($"{supabaseUrl}/rest/v1/daily_prices", httpContent);
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        var error = await response.Content.ReadAsStringAsync();
                        // 這裡可以記錄 Log，暫時先 return error
                        return StatusCode((int)response.StatusCode, new { error = $"Failed to sync batch: {error}" });
                    }
                }

                return Ok(new { message = $"Synced {dailyPrices.Count} daily price records successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // 定義 TWSE 回傳的資料結構
        private class TwseStockDayData
        {
            public string Code { get; set; }
            public string Name { get; set; }
            public string TradeVolume { get; set; }
            public string TradeValue { get; set; }
            public string OpeningPrice { get; set; }
            public string HighestPrice { get; set; }
            public string LowestPrice { get; set; }
            public string ClosingPrice { get; set; }
            public string Change { get; set; }
            public string Transaction { get; set; }
        }
    }
}
