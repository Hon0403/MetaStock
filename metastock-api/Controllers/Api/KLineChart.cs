using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;

namespace MetaStock.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class KLineChart : ControllerBase
    {
        private readonly HttpClient _httpClient;
        public KLineChart(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient();
        }
        // AJAX: 取得K線資料
        [HttpGet]
        public async Task<IActionResult> GetYahooKLineData(string stockId, string timeframe)
        {

            try
            {
                // 台股格式：股票代號.TW
                var symbol = $"{stockId}.TW";
                var range = GetYahooRange(timeframe); // 1mo, 3mo, 6mo, 1y

                // Yahoo Finance Chart API
                var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{symbol}?range={range}&interval=1d";

                // 設置請求標頭避免封鎖
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

                var response = await _httpClient.GetStringAsync(url);
                var data = JsonSerializer.Deserialize<JsonElement>(response);
                return Ok(ProcessYahooFinanceData(data, timeframe));
            }
            catch (Exception ex)
            {
                return Ok(new { error = ex.Message });
            }
        }

        private string GetYahooRange(string timeframe)
        {
            return timeframe.ToUpper() switch
            {
                "7D" => "5d",     // 近5日
                "1M" => "1mo",    // 近1月
                "3M" => "3mo",    // 近3月
                "6M" => "6mo",    // 近半年
                "1Y" => "1y",     // 近1年
                "2Y" => "2y",     // 近2年
                "5Y" => "5y",     // 近5年
                "YTD" => "ytd",   // 年初至今
                "MAX" => "max",   // 全部歷史
                _ => "3mo"        // 預設3個月
            };
        }


        private object ProcessYahooFinanceData(JsonElement yahooData, string timeframe)
        {
            var result = yahooData.GetProperty("chart").GetProperty("result")[0];
            var timestamps = result.GetProperty("timestamp").EnumerateArray();
            var quotes = result.GetProperty("indicators").GetProperty("quote")[0];

            var opens = quotes.GetProperty("open").EnumerateArray();
            var highs = quotes.GetProperty("high").EnumerateArray();
            var lows = quotes.GetProperty("low").EnumerateArray();
            var closes = quotes.GetProperty("close").EnumerateArray();
            var volumes = quotes.GetProperty("volume").EnumerateArray();

            // 直接產生ECharts需要的格式
            var dates = new List<string>();
            var klineData = new List<decimal[]>();
            var volumeData = new List<decimal>();

            var timestampArray = timestamps.ToArray();
            var openArray = opens.ToArray();
            var highArray = highs.ToArray();
            var lowArray = lows.ToArray();
            var closeArray = closes.ToArray();
            var volumeArray = volumes.ToArray();

            for (int i = 0; i < timestampArray.Length; i++)
            {
                if (openArray[i].ValueKind == JsonValueKind.Null) continue;

                // 日期轉換為ECharts格式
                var timestamp = timestampArray[i].GetInt64();
                var date = DateTimeOffset.FromUnixTimeSeconds(timestamp).ToString("yyyy-MM-dd");
                dates.Add(date);

                // OHLC資料：[開盤, 收盤, 最低, 最高]
                klineData.Add(new decimal[] {
                    openArray[i].GetDecimal(),   // 開盤
                    closeArray[i].GetDecimal(),  // 收盤
                    lowArray[i].GetDecimal(),    // 最低
                    highArray[i].GetDecimal()    // 最高
                });

                // 成交量（轉為千股）
                volumeData.Add(volumeArray[i].GetDecimal() / 1000);
            }
            Debug.WriteLine(dates);

            return new
            {
                dates = dates,          // 前端直接使用
                klineData = klineData,  // 前端直接使用
                volumes = volumeData,   // 前端直接使用
                timeframe = timeframe,
                total = dates.Count,
                source = "Yahoo Finance",
                symbol = result.GetProperty("meta").GetProperty("symbol").GetString()
            };
        }
    }
}
