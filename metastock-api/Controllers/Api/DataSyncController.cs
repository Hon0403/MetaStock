using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using Npgsql;

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
                // 1. 取得 PostgreSQL 連線字串
                var connectionString = _configuration.GetConnectionString("PostgreSQL");
                if (string.IsNullOrEmpty(connectionString))
                {
                    return BadRequest("PostgreSQL connection string not configured");
                }

                // 2. 從證交所取得每日收盤行情 (全部股票)
                var twseResponse = await _httpClient.GetAsync("https://openapi.twse.com.tw/v1/exchangeReport/STOCK_DAY_ALL");
                twseResponse.EnsureSuccessStatusCode();

                var content = await twseResponse.Content.ReadAsStringAsync();
                var twseData = JsonSerializer.Deserialize<List<TwseStockDayData>>(content);

                if (twseData == null || !twseData.Any())
                {
                    return BadRequest("No data fetched from TWSE");
                }

                // 3. 資料轉換
                var today = DateTime.Today;
                var records = new List<(string stockId, decimal open, decimal high, decimal low, decimal close, long volume, decimal change, decimal changePct)>();

                foreach (var item in twseData)
                {
                    if (item.Code.Length > 4) continue;

                    if (decimal.TryParse(item.OpeningPrice, out var open) &&
                        decimal.TryParse(item.HighestPrice, out var high) &&
                        decimal.TryParse(item.LowestPrice, out var low) &&
                        decimal.TryParse(item.ClosingPrice, out var close) &&
                        long.TryParse(item.TradeVolume, out var volume) &&
                        decimal.TryParse(item.Change, out var change))
                    {
                        var changePct = (close - change) != 0
                            ? Math.Round(change / (close - change) * 100, 2) : 0;
                        records.Add((item.Code, open, high, low, close, volume, change, changePct));
                    }
                }

                // 4. 分批寫入 PostgreSQL (INSERT ON CONFLICT = Upsert)
                await using var conn = new NpgsqlConnection(connectionString);
                await conn.OpenAsync();

                foreach (var batch in records.Chunk(500))
                {
                    var sb = new StringBuilder();
                    sb.Append("INSERT INTO prices (stock_id, date, open, high, low, close, volume, change, change_pct) VALUES ");

                    var parameters = new List<NpgsqlParameter>();
                    int idx = 0;

                    for (int i = 0; i < batch.Length; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        sb.Append($"(@p{idx}, @p{idx + 1}, @p{idx + 2}, @p{idx + 3}, @p{idx + 4}, @p{idx + 5}, @p{idx + 6}, @p{idx + 7}, @p{idx + 8})");

                        var r = batch[i];
                        parameters.Add(new NpgsqlParameter($"@p{idx++}", r.stockId));
                        parameters.Add(new NpgsqlParameter($"@p{idx++}", today));
                        parameters.Add(new NpgsqlParameter($"@p{idx++}", r.open));
                        parameters.Add(new NpgsqlParameter($"@p{idx++}", r.high));
                        parameters.Add(new NpgsqlParameter($"@p{idx++}", r.low));
                        parameters.Add(new NpgsqlParameter($"@p{idx++}", r.close));
                        parameters.Add(new NpgsqlParameter($"@p{idx++}", r.volume));
                        parameters.Add(new NpgsqlParameter($"@p{idx++}", r.change));
                        parameters.Add(new NpgsqlParameter($"@p{idx++}", r.changePct));
                    }

                    sb.Append(" ON CONFLICT (stock_id, date) DO UPDATE SET ");
                    sb.Append("open = EXCLUDED.open, high = EXCLUDED.high, low = EXCLUDED.low, ");
                    sb.Append("close = EXCLUDED.close, volume = EXCLUDED.volume, ");
                    sb.Append("change = EXCLUDED.change, change_pct = EXCLUDED.change_pct");

                    await using var cmd = new NpgsqlCommand(sb.ToString(), conn);
                    cmd.Parameters.AddRange(parameters.ToArray());
                    await cmd.ExecuteNonQueryAsync();
                }

                return Ok(new { message = $"Synced {records.Count} daily price records successfully" });
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
