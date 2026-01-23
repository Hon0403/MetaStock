using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace MetaStock.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class MockRealtimePrice : ControllerBase
    {
        private static readonly Random _random = new Random();
        private static decimal _basePrice = 1295.0m;
        private static readonly List<MockDataPoint> _historicalData = new();
        private static int _replayIndex = 0;

        static MockRealtimePrice()
        {
            InitializeReplayData();
        }

        // ✅ 完全模擬證交所API的回傳格式
        [HttpGet]
        public IActionResult GetMockRealtimePrice(string stockId = "2330")
        {
            try
            {
                if (_replayIndex >= _historicalData.Count)
                    _replayIndex = 0;

                var current = _historicalData[_replayIndex++];

                // ✅ 生成當前這一筆的真實OHLC資料
                var realtimeOHLC = GenerateRealtimeOHLC(current);

                var mockResponse = new
                {
                    msgArray = new[]
                    {
                new
                {
                    c = stockId,
                    n = "台積電",
                    nf = "台灣積體電路製造股份有限公司",
                    ex = "tse",

                    // ✅ 真實的OHLC資料（模擬盤中波動）
                    z = realtimeOHLC.Close.ToString("F4"),     // 現價（最新成交價）
                    y = current.PrevClose.ToString("F4"),     // 昨收價
                    o = realtimeOHLC.Open.ToString("F4"),     // 今日開盤價
                    h = realtimeOHLC.High.ToString("F4"),     // 今日最高價
                    l = realtimeOHLC.Low.ToString("F4"),      // 今日最低價
                    
                    v = current.Volume.ToString(),
                    t = DateTime.Now.ToString("HH:mm:ss"),
                    d = DateTime.Now.ToString("yyyyMMdd"),

                    // 其他欄位...
                    a = $"{(realtimeOHLC.Close + 5):F4}_{(realtimeOHLC.Close + 10):F4}_",
                    b = $"{(realtimeOHLC.Close - 5):F4}_{(realtimeOHLC.Close - 10):F4}_",
                    ch = $"{stockId}.tw"
                }
            },
                    referer = "",
                    userDelay = 5000,
                    rtcode = "0000",
                    rtmessage = "OK"
                };

                return Ok(mockResponse);
            }
            catch (Exception ex)
            {
                return Ok(new { error = ex.Message });
            }
        }

        // ✅ 初始化模擬歷史資料
        private static void InitializeReplayData()
        {
            // 生成1000個模擬的台積電價格資料點
            for (int i = 0; i < 1000; i++)
            {
                // 模擬真實的價格波動
                var movement = (decimal)(_random.NextDouble() - 0.5) * 30; // ±15元
                _basePrice = Math.Max(1200, Math.Min(1400, _basePrice + movement));

                var open = _basePrice;
                var close = _basePrice + (decimal)(_random.NextDouble() - 0.5) * 10; // 收盤價變動
                var high = Math.Max(open, close) + (decimal)_random.NextDouble() * 5;
                var low = Math.Min(open, close) - (decimal)_random.NextDouble() * 5;

                _historicalData.Add(new MockDataPoint
                {
                    Open = open,
                    High = high,
                    Low = low,
                    Close = close,
                    Volume = _random.Next(10000, 100000),
                    PrevClose = _basePrice
                });

                _basePrice = close;
            }

            Console.WriteLine($"✅ 初始化 {_historicalData.Count} 個模擬資料點");
        }
        private static RealtimeOHLC GenerateRealtimeOHLC(MockDataPoint baseData)
        {
            var random = new Random();

            // 基於基礎資料生成盤中波動
            var openPrice = baseData.Open;

            // 模擬盤中價格波動（最新成交價）
            var priceMovement = (decimal)(random.NextDouble() - 0.5) * 20; // ±10元波動
            var currentPrice = Math.Max(1200, Math.Min(1400, baseData.Close + priceMovement));

            // 生成今日的高低價（累計最高最低）
            var todayHigh = Math.Max(openPrice, currentPrice) + (decimal)random.NextDouble() * 10;
            var todayLow = Math.Min(openPrice, currentPrice) - (decimal)random.NextDouble() * 10;

            return new RealtimeOHLC
            {
                Open = openPrice,
                High = todayHigh,
                Low = todayLow,
                Close = currentPrice  // 最新成交價
            };
        }
    }

    public class MockDataPoint
    {
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public int Volume { get; set; }
        public decimal PrevClose { get; set; }
    }

    public class RealtimeOHLC
    {
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
    }
}
