using Microsoft.Extensions.Configuration;
using MetaStockSync;

// 設定 UTF-8 編碼
Console.OutputEncoding = System.Text.Encoding.UTF8;

// 1. 建立基礎物件
var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();
var connectionString = config["PG_CONNECTION"]
    ?? throw new InvalidOperationException("請設定環境變數 PG_CONNECTION");

var api = new StockApiClient();
var repo = new StockRepository(connectionString);

// 2. 解析參數
var stockId = GetArg(args, "--stock");
var weeks = int.Parse(GetArg(args, "--weeks") ?? "1");
var updateFinancials = args.Contains("--financials");

Console.WriteLine($"開始每日同步... 目標: {(string.IsNullOrEmpty(stockId) ? "全市場" : stockId)}, 回朔週數: {weeks}");

// 3. 特殊模式：只更新財報就結束
if (updateFinancials)
{
    var financials = await api.FetchFinancialsAsync();
    await repo.BatchSaveAsync(financials, "財務報表");
    Console.WriteLine("財報更新完成。");
    return;
}

// 4. 前置：抓股票清單 + 產業分類 + 今日股價
var industryStocks = await api.FetchCompanyDetailsAsync();
var (allStocks, latestPrices) = await api.FetchStockDayAsync();

// 合併產業資訊
foreach (var stock in allStocks)
{
    var industryInfo = industryStocks.FirstOrDefault(s => s.StockId == stock.StockId);
    if (industryInfo != null)
        stock.Industry = industryInfo.Industry;
}

await repo.BatchSaveAsync(allStocks, "股票清單");
await repo.BatchSaveAsync(latestPrices, "今日股價");

// 5. 決定 targets（要處理哪些股票）
var targets = string.IsNullOrEmpty(stockId)
    ? allStocks
    : allStocks.Where(s => s.StockId == stockId).ToList();

if (!targets.Any())
{
    Console.WriteLine($"找不到股票 {stockId}");
    return;
}

// 6. 準備日期集合
var tradingDays = BuildRecentTradingDays(weeks * 5); // 每週 5 個交易日
var fridays = BuildRecentFridays(weeks);
var weekRanges = BuildWeekRanges(fridays);

// 7. 建立 Pipeline 清單
var pipelines = new List<Task>
{
    RunMarginsPipeline(tradingDays, api, repo),
    RunValuationPipeline(tradingDays, api, repo),
    RunDayTradesPipeline(tradingDays, api, repo),
    RunDividendsPipeline(weekRanges, api, repo),
    RunRevenuePipeline(api, repo),
    RunShareholdersPipeline(targets, fridays, api, repo),
    RunPriceHistoryPipeline(targets, fridays, api, repo)
};

// 券商分點：僅在指定股票時才跑
if (!string.IsNullOrEmpty(stockId))
{
    pipelines.Add(RunBrokerTradesPipeline(stockId, api, repo));
}

// 8. 啟動並行 Pipeline
Console.WriteLine($"啟動 {pipelines.Count} 條 Pipeline 並行抓取...");
await Task.WhenAll(pipelines);

Console.WriteLine("\n所有 Pipeline 完成！");

// ============================================================
// Pipeline 方法
// ============================================================

static async Task RunMarginsPipeline(
    List<DateTime> dates,
    StockApiClient api,
    StockRepository repo)
{
    Console.WriteLine($"[融資融券] 開始，共 {dates.Count} 天");
    foreach (var date in dates)
    {
        try
        {
            if (await repo.ExistsAsync(new Margins { Date = date }))
            {
                continue; // 已有資料，跳過
            }
            var data = await api.FetchMarginTradingAsync(date);
            await repo.BatchSaveAsync(data, "融資融券");
        }
        catch { /* 假日無資料，略過 */ }
        await Task.Delay(7000);
    }
    Console.WriteLine("[融資融券] Pipeline 完成");
}

static async Task RunValuationPipeline(
    List<DateTime> dates,
    StockApiClient api,
    StockRepository repo)
{
    Console.WriteLine($"[本益比] 開始，共 {dates.Count} 天");
    foreach (var date in dates)
    {
        try
        {
            if (await repo.ExistsAsync(new Valuation { Date = date }))
            {
                continue;
            }
            var data = await api.FetchValuationsAsync(date);
            await repo.BatchSaveAsync(data, "本益比、殖利率、股價淨值比");
        }
        catch { /* 假日無資料，略過 */ }
        await Task.Delay(7000);
    }
    Console.WriteLine("[本益比] Pipeline 完成");
}

static async Task RunDayTradesPipeline(
    List<DateTime> dates,
    StockApiClient api,
    StockRepository repo)
{
    Console.WriteLine($"[當沖] 開始，共 {dates.Count} 天");
    foreach (var date in dates)
    {
        try
        {
            if (await repo.ExistsAsync(new DayTrade { Date = date }))
            {
                continue;
            }
            var data = await api.FetchDayTradesAsync(date);
            await repo.BatchSaveAsync(data, "當日沖銷交易");
        }
        catch { /* 假日無資料，略過 */ }
        await Task.Delay(7000);
    }
    Console.WriteLine("[當沖] Pipeline 完成");
}

static async Task RunDividendsPipeline(
    List<(DateTime WeekStart, DateTime WeekEnd)> weeks,
    StockApiClient api,
    StockRepository repo)
{
    Console.WriteLine($"[除權息] 開始，共 {weeks.Count} 週");
    foreach (var (weekStart, weekEnd) in weeks)
    {
        try
        {
            var data = await api.FetchDividendsAsync(weekStart, weekEnd);
            await repo.BatchSaveAsync(data, "除權息公告");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[除權息] {weekStart:MM/dd}~{weekEnd:MM/dd} 失敗: {ex.Message}");
        }
        await Task.Delay(7000);
    }
    Console.WriteLine("[除權息] Pipeline 完成");
}

static async Task RunRevenuePipeline(
    StockApiClient api,
    StockRepository repo)
{
    Console.WriteLine("[月營收] 開始");
    try
    {
        var data = await api.FetchRevenueAsync(DateTime.Today);
        await repo.BatchSaveAsync(data, "月營收");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[月營收] 失敗: {ex.Message}");
    }
    Console.WriteLine("[月營收] Pipeline 完成");
}

static async Task RunShareholdersPipeline(
    List<StockInfo> stocks,
    List<DateTime> weekDates,
    StockApiClient api,
    StockRepository repo)
{
    Console.WriteLine($"[集保戶] 開始，{stocks.Count} 檔 × {weekDates.Count} 週");
    int count = 0;
    foreach (var queryDate in weekDates)
    {
        foreach (var stock in stocks)
        {
            try
            {
                // 已有資料就跳過
                if (await repo.ExistsAsync(new Shareholders { StockId = stock.StockId, Date = queryDate }))
                {
                    continue;
                }

                var data = await api.FetchSingleStockHistoryAsync(stock.StockId, queryDate);
                if (data.Count > 0)
                    await repo.BatchSaveAsync(data, "集保戶");

                count++;
                if (count % 50 == 0)
                    Console.WriteLine($"[集保戶] 已處理 {count} 筆...");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[集保戶] {stock.StockId} {queryDate:MM/dd} 失敗: {ex.Message}");
            }
            await Task.Delay(7000);
        }
    }
    Console.WriteLine($"[集保戶] Pipeline 完成，共處理 {count} 筆");
}

static async Task RunPriceHistoryPipeline(
    List<StockInfo> stocks,
    List<DateTime> weekDates,
    StockApiClient api,
    StockRepository repo)
{
    Console.WriteLine($"[歷史股價] 開始，{stocks.Count} 檔 × {weekDates.Count} 週");
    int count = 0;
    foreach (var queryDate in weekDates)
    {
        foreach (var stock in stocks)
        {
            try
            {
                // 已有資料就跳過
                if (await repo.ExistsAsync(new DailyPrice { StockId = stock.StockId, Date = queryDate }))
                {
                    continue;
                }

                var data = await api.FetchPriceHistoryAsync(stock.StockId, queryDate);
                if (data.Count > 0)
                    await repo.BatchSaveAsync(data, "歷史股價");

                count++;
                if (count % 50 == 0)
                    Console.WriteLine($"[歷史股價] 已處理 {count} 筆...");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[歷史股價] {stock.StockId} {queryDate:MM/dd} 失敗: {ex.Message}");
            }
            await Task.Delay(7000);
        }
    }
    Console.WriteLine($"[歷史股價] Pipeline 完成，共處理 {count} 筆");
}

static async Task RunBrokerTradesPipeline(
    string stockId,
    StockApiClient api,
    StockRepository repo)
{
    Console.WriteLine($"[券商分點] 開始抓取 {stockId}...");
    try
    {
        var data = await api.FetchBrokerTradesAsync(stockId, DateTime.Today);
        await repo.BatchSaveAsync(data, "券商分點買賣日報");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[券商分點] {stockId} 失敗: {ex.Message}");
    }
    Console.WriteLine("[券商分點] Pipeline 完成");
}

// ============================================================
// 輔助函式
// ============================================================

static string? GetArg(string[] args, string key)
{
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == key) return args[i + 1];
    }
    return null;
}

static List<DateTime> BuildRecentTradingDays(int days)
{
    var list = new List<DateTime>();
    var d = DateTime.Today;
    while (list.Count < days)
    {
        if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday)
            list.Add(d);
        d = d.AddDays(-1);
    }
    list.Reverse();
    return list;
}

static List<DateTime> BuildRecentFridays(int weeks)
{
    var list = new List<DateTime>();
    var date = GetLastFriday(DateTime.Today);

    for (int w = 0; w < weeks; w++)
    {
        list.Add(date.AddDays(-7 * w));
    }

    list.Reverse();
    return list;
}

static DateTime GetLastFriday(DateTime date)
{
    while (date.DayOfWeek != DayOfWeek.Friday)
    {
        date = date.AddDays(-1);
    }
    return date;
}

static List<(DateTime WeekStart, DateTime WeekEnd)> BuildWeekRanges(List<DateTime> fridays)
{
    return fridays
        .Select(f => (WeekStart: f.AddDays(-4), WeekEnd: f))
        .ToList();
}
