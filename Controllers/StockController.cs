using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace MetaStock.Controllers
{
    public class StockController : Controller
    {
        private readonly HttpClient _httpClient;
        private readonly Dictionary<string, string> _stockNames;

        public StockController(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient();

            // 股票名稱對照表
            _stockNames = new Dictionary<string, string>
            {
                {"2330", "台積電"}, {"2317", "鴻海"}, {"2454", "聯發科"},
                {"2882", "國泰金"}, {"2884", "玉山金"}, {"2412", "中華電"}
            };
        }

        // GET: /Stock/Detail/2330
        public IActionResult Detail(string id = "2330")
        {
            ViewBag.StockId = id;
            ViewBag.Title = $"股票資訊 - {id}";
            return View();
        }

        // AJAX: 取得股票基本資料
        [HttpGet]
        public async Task<IActionResult> GetStockData(string stockId)
        {
            try
            {
                var stockData = await FetchStockDataFromTwse(stockId);
                var stockInfo = ProcessStockInfo(stockData, stockId);
                return Json(stockInfo);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // AJAX: 取得K線資料
        [HttpGet]
        public async Task<IActionResult> GetKLineData(string stockId, string timeframe = "D")
        {
            try
            {
                var stockData = await FetchStockDataFromTwse(stockId);
                var klineData = ProcessKLineData(stockData);
                return Json(klineData);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // AJAX: 取得歷史資料
        [HttpGet]
        public async Task<IActionResult> GetHistoricalData(string stockId)
        {
            try
            {
                var stockData = await FetchStockDataFromTwse(stockId);
                var historicalData = ProcessHistoricalData(stockData);
                return Json(historicalData);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // 私有方法：從證交所取得原始資料
        private async Task<dynamic> FetchStockDataFromTwse(string stockId)
        {
            var currentMonth = DateTime.Now.ToString("yyyyMM");
            var url = $"https://www.twse.com.tw/exchangeReport/STOCK_DAY?response=json&date={currentMonth}&stockNo={stockId}";

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            var response = await _httpClient.GetStringAsync(url);
            return JsonSerializer.Deserialize<dynamic>(response);
        }

        // 私有方法：處理股票基本資訊
        private object ProcessStockInfo(dynamic data, string stockId)
        {
            var dataArray = ((JsonElement)data).GetProperty("data");
            if (dataArray.GetArrayLength() == 0)
                throw new Exception("無資料");

            var latestData = dataArray[dataArray.GetArrayLength() - 1];
            var stockName = _stockNames.ContainsKey(stockId) ? _stockNames[stockId] : "未知股票";

            var currentPrice = ParseDecimal(latestData[6].GetString());
            var openPrice = ParseDecimal(latestData[3].GetString());
            var highPrice = ParseDecimal(latestData[4].GetString());
            var lowPrice = ParseDecimal(latestData[5].GetString());

            // 計算漲跌
            var prevPrice = dataArray.GetArrayLength() > 1 ?
                ParseDecimal(dataArray[dataArray.GetArrayLength() - 2][6].GetString()) : currentPrice;
            var change = currentPrice - prevPrice;
            var changePercent = prevPrice != 0 ? (change / prevPrice * 100).ToString("F2") + "%" : "0.00%";
            var priceChangeClass = change > 0 ? "price-up" : change < 0 ? "price-down" : "price-neutral";

            return new
            {
                stockId = stockId,
                stockName = stockName,
                currentPrice = currentPrice.ToString("F2"),
                priceChange = $"{(change >= 0 ? "+" : "")}{change:F2} ({(change >= 0 ? "+" : "")}{changePercent})",
                priceChangeClass = priceChangeClass,
                volume = (ParseLong(latestData[1].GetString()) / 1000).ToString("#,0"),
                pe = "N/A",
                openPrice = openPrice.ToString("F2"),
                highPrice = highPrice.ToString("F2"),
                lowPrice = lowPrice.ToString("F2"),
                prevClose = prevPrice.ToString("F2")
            };
        }

        // 私有方法：處理K線資料
        private object ProcessKLineData(dynamic data)
        {
            var dataArray = ((JsonElement)data).GetProperty("data");
            var dates = new List<string>();
            var klineData = new List<decimal[]>();
            var volumes = new List<long>();

            foreach (var item in dataArray.EnumerateArray())
            {
                dates.Add(ConvertTaiwanDateToGregorian(item[0].GetString()));

                var open = ParseDecimal(item[3].GetString());
                var high = ParseDecimal(item[4].GetString());
                var low = ParseDecimal(item[5].GetString());
                var close = ParseDecimal(item[6].GetString());

                klineData.Add(new[] { open, close, low, high });
                volumes.Add(ParseLong(item[1].GetString()) / 1000);
            }

            return new { dates, klineData, volumes };
        }

        // 私有方法：處理歷史資料
        private object ProcessHistoricalData(dynamic data)
        {
            var dataArray = ((JsonElement)data).GetProperty("data");
            var result = new List<object>();
            decimal prevClose = 0;

            foreach (var item in dataArray.EnumerateArray())
            {
                var close = ParseDecimal(item[6].GetString());
                var change = prevClose != 0 ? close - prevClose : 0;
                var changePercent = prevClose != 0 ? (change / prevClose * 100).ToString("F2") + "%" : "0.00%";

                result.Add(new
                {
                    date = ConvertTaiwanDateToGregorian(item[0].GetString()),
                    open = ParseDecimal(item[3].GetString()).ToString("F2"),
                    high = ParseDecimal(item[4].GetString()).ToString("F2"),
                    low = ParseDecimal(item[5].GetString()).ToString("F2"),
                    close = close.ToString("F2"),
                    volume = (ParseLong(item[1].GetString()) / 1000).ToString("#,0"),
                    change = change,
                    changePercent = $"{(change >= 0 ? "+" : "")}{changePercent}"
                });

                prevClose = close;
            }

            return result.OrderByDescending(x => ((dynamic)x).date);
        }

        // 輔助方法
        private decimal ParseDecimal(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value == "--") return 0;
            var cleanValue = value.Replace(",", "").Replace("+", "").Replace(" ", "");
            return decimal.TryParse(cleanValue, out var result) ? result : 0;
        }

        private long ParseLong(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value == "--") return 0;
            var cleanValue = value.Replace(",", "").Replace("+", "").Replace(" ", "");
            return long.TryParse(cleanValue, out var result) ? result : 0;
        }

        private string ConvertTaiwanDateToGregorian(string taiwanDate)
        {
            try
            {
                var parts = taiwanDate.Split('/');
                if (parts.Length == 3)
                {
                    var year = int.Parse(parts[0]) + 1911;
                    var month = int.Parse(parts[1]);
                    var day = int.Parse(parts[2]);
                    return new DateTime(year, month, day).ToString("yyyy-MM-dd");
                }
                return taiwanDate;
            }
            catch
            {
                return taiwanDate;
            }
        }
    }
}