using System.Text.Json;
using MetaStockSync;
using HtmlAgilityPack;
using System.Net;
using System.Globalization;

namespace MetaStockSync
{
    public class StockApiClient
    {
        private static readonly HttpClient _http = new HttpClient();
        private readonly CaptchaSolver _captchaSolver;

        public StockApiClient()
        {
            _captchaSolver = new CaptchaSolver();
        }

        // 抓取券商分點買賣日報 (含驗證碼自動破解)
        public async Task<List<BrokerTrade>> FetchBrokerTradesAsync(string stockId, DateTime date)
        {
            Console.WriteLine($"[爬蟲] 正在抓取 {stockId} 的分點資料 (自動解碼重試模式)...");

            string baseUrl = "https://bsr.twse.com.tw/bshtm/bsMenu.aspx";
            string contentUrl = "https://bsr.twse.com.tw/bshtm/bsContent.aspx"; // Keep this just in case, but we post to baseUrl

            int maxRetries = 5;
            for (int i = 0; i < maxRetries; i++)
            {
                // 將 HttpClient 移入迴圈內，確保每次重試都是全新的 Session 且會下載新驗證碼
                var handler = new HttpClientHandler { UseCookies = true, CookieContainer = new CookieContainer() };
                using var client = new HttpClient(handler);
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

                try
                {
                    // Step 1: GET bsMenu.aspx 取得 ViewState & Session
                    var menuRes = await client.GetAsync(baseUrl);
                    menuRes.EnsureSuccessStatusCode();
                    var menuHtml = await menuRes.Content.ReadAsStringAsync();

                    var doc = new HtmlDocument();
                    doc.LoadHtml(menuHtml);

                    var viewState = doc.DocumentNode.SelectSingleNode("//input[@name='__VIEWSTATE']")?.GetAttributeValue("value", "");
                    var eventValidation = doc.DocumentNode.SelectSingleNode("//input[@name='__EVENTVALIDATION']")?.GetAttributeValue("value", "");
                    var viewStateGenerator = doc.DocumentNode.SelectSingleNode("//input[@name='__VIEWSTATEGENERATOR']")?.GetAttributeValue("value", "");

                    // Step 2: GET Captcha Image (Dynamic URL)
                    // 解析 <img src='CaptchaImage.aspx?guid=...'>
                    var imgNode = doc.DocumentNode.SelectSingleNode("//img[contains(@src, 'CaptchaImage.aspx')]");
                    if (imgNode == null)
                    {
                        Console.WriteLine($"  ⚠️ 第 {i + 1} 次: 找不到驗證碼連結，重試中...");
                        continue;
                    }
                    var captchaSrc = imgNode.GetAttributeValue("src", "");
                    var fullCaptchaUrl = new Uri(new Uri(baseUrl), captchaSrc).ToString();

                    var captchaRes = await client.GetByteArrayAsync(fullCaptchaUrl);

                    // Step 3: Solve Captcha
                    string captchaCode = _captchaSolver.Solve(captchaRes);
                    if (string.IsNullOrWhiteSpace(captchaCode) || captchaCode.Length < 5)
                    {
                        Console.WriteLine($"  ⚠️ 第 {i + 1} 次: 辨識失敗 (長度不足: {captchaCode})，更換圖片重試...");
                        continue;
                    }

                    // Step 4: POST Query
                    // 注意: 根據 HTML form action="./bsMenu.aspx"，我們應該 POST 到 bsMenu.aspx
                    var formData = new Dictionary<string, string>
                    {
                        { "__EVENTTARGET", "" },
                        { "__EVENTARGUMENT", "" },
                        { "__LASTFOCUS", "" },
                        { "__VIEWSTATE", viewState ?? "" },
                        { "__VIEWSTATEGENERATOR", viewStateGenerator ?? "" },
                        { "__EVENTVALIDATION", eventValidation ?? "" },
                        { "RadioButton_Normal", "RadioButton_Normal" }, // 選擇 "個別證券"
                        { "TextBox_Stkno", stockId },
                        { "CaptchaControl1", captchaCode },
                        { "btnOK", "查詢" }
                    };

                    var content = new FormUrlEncodedContent(formData);
                    // POST 到 bsMenu.aspx
                    var postRes = await client.PostAsync(baseUrl, content);
                    postRes.EnsureSuccessStatusCode();
                    var resultHtml = await postRes.Content.ReadAsStringAsync();

                    // Step 5: Check Result and Redirect
                    // 通常 POST 成功後會顯示結果，或者 Redirect 到 bsContent.aspx
                    // 檢查是否還在 bsMenu (代表失敗或還在原頁)

                    if (resultHtml.Contains("驗證碼錯誤"))
                    {
                        Console.WriteLine($"  ⚠️ 第 {i + 1} 次: 驗證碼輸入錯誤 ({captchaCode})，更換圖片重試...");
                        continue;
                    }

                    if (resultHtml.Contains("查無資料"))
                    {
                        Console.WriteLine($"  ✅ 查詢成功，但本日無交易資料。");
                        return new List<BrokerTrade>();
                    }

                    // 如果成功，通常會看到 "HyperLink_DownloadCSV" 或者直接是 bsContent 的內容
                    // 有時候 Server 會回應 302 Found 轉址，HttpClient 會自動跟隨
                    // 如果 HttpClient 自動跟隨了，那我們現在的手上的 resultHtml 已經是 bsContent.aspx 的內容了

                    // Step 6: Parse Result (CSV)
                    // The site provides a CSV download link
                    var resultDoc = new HtmlDocument();
                    resultDoc.LoadHtml(resultHtml);

                    var csvLink = resultDoc.DocumentNode.SelectSingleNode("//a[@id='HyperLink_DownloadCSV']");

                    if (csvLink != null)
                    {
                        var csvHref = csvLink.GetAttributeValue("href", "");
                        var fullCsvUrl = new Uri(new Uri(baseUrl), csvHref).ToString();
                        Console.WriteLine($"  ✅ 取得 CSV 連結: {csvHref}");

                        var csvBytes = await client.GetByteArrayAsync(fullCsvUrl);

                        // TWSE CSV is usually Big5 (CP950)
                        // Note: .NET Core requires System.Text.Encoding.CodePages for Big5
                        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
                        var big5 = System.Text.Encoding.GetEncoding(950);
                        var csvContent = big5.GetString(csvBytes);

                        // Parse CSV
                        var trades = new List<BrokerTrade>();
                        var lines = csvContent.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                        bool headerFound = false;
                        foreach (var line in lines)
                        {
                            if (!headerFound)
                            {
                                if (line.Contains("序號,券商,價格"))
                                {
                                    headerFound = true;
                                }
                                continue;
                            }

                            // Format: 序號,券商,價格,買進股數,賣出股數,,序號,券商,價格,買進股數,賣出股數
                            var cols = line.Split(',');

                            // Parse Left Side (0-4)
                            if (cols.Length >= 5 && int.TryParse(cols[0], out _))
                            {
                                ParseTradeColumn(trades, stockId, date, cols, 0);
                            }

                            // Parse Right Side (6-10)
                            if (cols.Length >= 11 && int.TryParse(cols[6], out _))
                            {
                                ParseTradeColumn(trades, stockId, date, cols, 6);
                            }
                        }

                        if (trades.Any())
                        {
                            Console.WriteLine($"  ✅ 成功取得 {trades.Count} 筆分點資料！");
                            return trades;
                        }
                        else
                        {
                            Console.WriteLine($"  ⚠️ 解析後無資料 (CSV 空白?)");
                            return trades;
                        }
                    }
                    else
                    {
                        // 雖然沒說查無資料，但也沒解析到?
                        Console.WriteLine($"  ⚠️ 解析後無資料 (找不到 CSV 連結)，可能格式變更? HTML 已存至 debug_bs_result.html");
                        await File.WriteAllTextAsync("debug_bs_result.html", resultHtml);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ❌ 發生錯誤: {ex.Message}，重試中...");
                }

                // 休息一下再重試
                await Task.Delay(1000 + (i * 500));
            }

            Console.WriteLine($"❌ 重試 {maxRetries} 次後仍然失敗，放棄。");
            return new List<BrokerTrade>();
        }

        private void ParseTradeColumn(List<BrokerTrade> trades, string stockId, DateTime date, string[] cols, int offset)
        {
            try
            {
                // cols[offset+1] is Broker "1020合　　庫"
                string brokerStr = cols[offset + 1].Trim();
                string brokerId = brokerStr.Length >= 4 ? brokerStr.Substring(0, 4) : brokerStr;
                string brokerName = brokerStr.Length > 4 ? brokerStr.Substring(4).Trim() : "";

                decimal price = ParseDecimal(cols[offset + 2]);
                int buyVol = ParseInt(cols[offset + 3]);
                int sellVol = ParseInt(cols[offset + 4]);

                trades.Add(new BrokerTrade
                {
                    StockId = stockId,
                    Date = date,
                    BrokerId = brokerId,
                    BrokerName = brokerName,
                    Price = price,
                    BuyVolume = buyVol,
                    SellVolume = sellVol
                });
            }
            catch { }
        }

        private async Task<List<T>> FetchAndParseAsync<T>(
            string url,
            string dataPath,
            Func<JsonElement, T> parseItem,
            string itemTypeName)
        {
            try
            {
                // 固定：HTTP 請求
                Console.WriteLine($"正在抓取 {itemTypeName}...");
                var json = await _http.GetStringAsync(url);
                var doc = JsonDocument.Parse(json);

                // 固定：檢查狀態
                if (!doc.RootElement.TryGetProperty("stat", out var status)
                    || status.GetString() != "OK")
                {
                    var statStr = status.GetString() ?? "";
                    if (statStr.Contains("沒有") || statStr.Contains("無資料") || statStr.Contains("查無") || string.IsNullOrEmpty(statStr))
                        Console.WriteLine($"[提示] {itemTypeName} 無資料 (可能是假日或尚未結算)");
                    else
                        Console.WriteLine($"{itemTypeName} API 回應狀態異常: {statStr}");

                    return new List<T>();
                }

                var result = new List<T>();
                var dataArray = doc.RootElement.GetProperty(dataPath);

                foreach (var item in dataArray.EnumerateArray())
                {
                    var parsed = parseItem(item);
                    if (parsed == null) continue;
                    result.Add(parsed);
                }

                Console.WriteLine($"成功取得 {result.Count} 筆{itemTypeName}");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"抓取{itemTypeName}失敗: {ex.Message}");
                return new List<T>();
            }
        }

        private async Task<List<T>> FetchOpenDataArrayAsync<T>(
            string url,
            Func<JsonElement, T?> parseItem,
            string itemTypeName)
            where T : class
        {
            try
            {
                Console.WriteLine($"正在抓取 {itemTypeName}...");
                var json = await _http.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.ValueKind != JsonValueKind.Array)
                {
                    Console.WriteLine($"{itemTypeName} API 回傳格式不是陣列");
                    return new List<T>();
                }

                var list = new List<T>();
                foreach (var row in root.EnumerateArray())
                {
                    var item = parseItem(row);
                    if (item == null) continue;   // 過濾不要的列
                    list.Add(item);
                }

                Console.WriteLine($"已取得 {list.Count} 筆{itemTypeName}");
                return list;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"抓取{itemTypeName}失敗: {ex.Message}");
                return new List<T>();
            }
        }


        // 從證交所抓取每日收盤行情
        public async Task<(List<StockInfo>, List<DailyPrice>)> FetchStockDayAsync()
        {
            Console.WriteLine("正在從證交所抓取每日收盤行情...");
            var twseResponse = await _http.GetAsync("https://openapi.twse.com.tw/v1/exchangeReport/STOCK_DAY_ALL");
            twseResponse.EnsureSuccessStatusCode();

            var content = await twseResponse.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            var dailyPrices = new List<DailyPrice>();
            var stocksList = new List<StockInfo>();
            var today = DateTime.Today;

            foreach (var item in root.EnumerateArray())
            {
                var code = item.GetProperty("Code").GetString();
                var name = item.GetProperty("Name").GetString();

                if (!IsValidStockId(code)) continue;


                stocksList.Add(new StockInfo
                {
                    StockId = code,
                    Name = name,
                    Market = "TSE"
                });


                if (decimal.TryParse(item.GetProperty("OpeningPrice").GetString(), out var open) &&
                    decimal.TryParse(item.GetProperty("HighestPrice").GetString(), out var high) &&
                    decimal.TryParse(item.GetProperty("LowestPrice").GetString(), out var low) &&
                    decimal.TryParse(item.GetProperty("ClosingPrice").GetString(), out var close) &&
                    long.TryParse(item.GetProperty("TradeVolume").GetString(), out var volume) &&
                    decimal.TryParse(item.GetProperty("Change").GetString(), out var change))
                {
                    var changePercent = (close - change) != 0 ? Math.Round(change / (close - change) * 100, 2) : 0;

                    dailyPrices.Add(new DailyPrice
                    {
                        StockId = code,
                        Date = today,
                        Open = open,
                        High = high,
                        Low = low,
                        Close = close,
                        Volume = volume,
                        Change = change,
                        ChangePct = changePercent
                    });
                }
            }
            Console.WriteLine($"已準備 {stocksList.Count} 檔股票與 {dailyPrices.Count} 筆股價資料。");
            return (stocksList, dailyPrices);
        }

        // 從證交所抓取三大法人買賣超
        public async Task<List<InstitutionalTrade>> FetchInstitutionalAsync(DateTime date)
        {
            var todayStr = date.ToString("yyyyMMdd");
            return await FetchAndParseAsync<InstitutionalTrade>(
                url: $"https://www.twse.com.tw/rwd/zh/fund/T86?date={todayStr}&selectType=ALL&response=json",
                dataPath: "data",
                parseItem: row =>
                {
                    var code = row[0].GetString();
                    if (!IsValidStockId(code))
                    {
                        // 不合法的股票代號，回傳 null 讓外層過濾掉
                        return null;
                    }
                    return new InstitutionalTrade
                    {
                        StockId = code,
                        Date = date,
                        ForeignNet = ParseInt(row[4].GetString()),
                        TrustNet = ParseInt(row[10].GetString()),
                        DealerNet = ParseInt(row[11].GetString()),
                        TotalNet = ParseInt(row[18].GetString())
                    };
                },
                itemTypeName: "法人買賣超"
            );
        }

        // 抓取指定月份的營收
        public async Task<List<MonthlyRevenue>> FetchRevenueAsync(DateTime date)
        {
            var rocYear = date.Year - 1911;  // 民國年
            var month = date.Month;

            // MOPS URL (上市公司) - 注意網址是 mopsov
            var url = $"https://mopsov.twse.com.tw/nas/t21/sii/t21sc03_{rocYear}_{month}_0.html";

            Console.WriteLine($"正在抓取 {rocYear} 年 {month} 月營收...");

            var html = await _http.GetStringAsync(url);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var list = new List<MonthlyRevenue>();

            // 找到表格的所有 tr
            var rows = doc.DocumentNode.SelectNodes("//table[@class='hasBorder']//tr");
            if (rows == null) return list;

            foreach (var row in rows.Skip(1)) // 跳過標題
            {
                var cols = row.SelectNodes("td");
                if (cols == null || cols.Count < 10) continue;

                var code = cols[0].InnerText.Trim();
                if (code.Length > 4) continue; // 過濾非股票

                list.Add(new MonthlyRevenue
                {
                    StockId = code,
                    Year = date.Year,
                    Month = month,
                    Revenue = ParseDecimal(cols[2].InnerText),  // 當月營收
                    MomPct = ParseDecimal(cols[6].InnerText),   // 月增率
                    YoyPct = ParseDecimal(cols[8].InnerText)    // 年增率
                });
            }

            Console.WriteLine($"已解析 {list.Count} 筆營收紀錄。");
            return list;
        }

        // 抓取單一股票指定日期的集保戶資料
        public async Task<List<Shareholders>> FetchSingleStockHistoryAsync(string stockId, DateTime queryDate)
        {
            var dateStr = queryDate.ToString("yyyyMMdd");
            Console.WriteLine($"[集保戶] 正在查詢 {stockId} 於 {dateStr} 的資料...");

            var resultList = new List<Shareholders>();
            // Update URL to the new one
            var url = "https://www.tdcc.com.tw/portal/zh/smWeb/qryStock";

            try
            {
                // 使用獨立的 HttpClient 與 CookieContainer 確保 session 獨立
                var handler = new HttpClientHandler { UseCookies = true, CookieContainer = new CookieContainer() };
                using var localClient = new HttpClient(handler);
                localClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

                // Step 1: 取得初始頁面與 ViewState
                var initResponse = await localClient.GetAsync(url);
                initResponse.EnsureSuccessStatusCode();
                var initHtml = await initResponse.Content.ReadAsStringAsync();

                var doc = new HtmlDocument();
                doc.LoadHtml(initHtml);

                // Step 2: 取出新的隱藏欄位 (SYNCHRONIZER)
                var syncToken = doc.DocumentNode.SelectSingleNode("//input[@id='SYNCHRONIZER_TOKEN']")?.GetAttributeValue("value", "");
                var syncUri = doc.DocumentNode.SelectSingleNode("//input[@id='SYNCHRONIZER_URI']")?.GetAttributeValue("value", "");
                var method = doc.DocumentNode.SelectSingleNode("//input[@id='method']")?.GetAttributeValue("value", "");
                var firDate = doc.DocumentNode.SelectSingleNode("//input[@id='firDate']")?.GetAttributeValue("value", "");

                // Step 3: 建構 POST 表單資料
                var formData = new Dictionary<string, string>
                {
                    { "SYNCHRONIZER_TOKEN", syncToken ?? "" },
                    { "SYNCHRONIZER_URI", syncUri ?? "/portal/zh/smWeb/qryStock" },
                    { "method", method ?? "submit" },
                    { "firDate", firDate ?? dateStr },
                    { "scaDate", dateStr },
                    { "sqlMethod", "StockNo" },
                    { "stockNo", stockId },
                    { "stockName", "" }
                };

                var content = new FormUrlEncodedContent(formData);

                // await Task.Delay(3000); // 移除不必要的固定延遲，改為安全間隔
                await Task.Delay(1000); // 防火牆保護短暫延遲

                var postResponse = await localClient.PostAsync(url, content);
                postResponse.EnsureSuccessStatusCode();
                var resultHtml = await postResponse.Content.ReadAsStringAsync();

                // Step 4: 解析回傳的 HTML 表格
                var resultDoc = new HtmlDocument();
                resultDoc.LoadHtml(resultHtml);

                // 尋找表格列 (tr)
                var tables = resultDoc.DocumentNode.SelectNodes("//table");
                var rows = tables != null && tables.Count > 1
                    ? tables[1].SelectNodes(".//tr")
                    : tables?.FirstOrDefault()?.SelectNodes(".//tr");

                if (rows == null || rows.Count == 0)
                {
                    Console.WriteLine("抓不到表格！可能是參數錯誤或 IP 被擋。");
                    return resultList;
                }

                Console.WriteLine($"取得 {rows.Count} 列資料，開始解析...");

                var shareholderData = new Shareholders
                {
                    StockId = stockId,
                    Date = queryDate,
                    TotalShare = 0
                };

                // 跳過標題列，開始解析
                foreach (var row in rows.Skip(1))
                {
                    var cols = row.SelectNodes("td");

                    if (cols == null || cols.Count < 4) continue;

                    var levelText = cols[0].InnerText.Trim();
                    var sharesText = cols[3].InnerText.Trim(); // index 2 is 人數(accounts), index 3 is 股數(shares)


                    if (!int.TryParse(levelText, out int level)) continue;


                    if (!long.TryParse(sharesText.Replace(",", ""), out long shares)) continue;

                    shareholderData.TotalShare += shares;


                    if (level >= 1 && level <= 5)
                    {
                        shareholderData.RetailShare += shares;
                    }
                    else if (level >= 13 && level <= 15)
                    {
                        shareholderData.BigShare += shares;

                        if (level == 15)
                        {
                            shareholderData.SuperShare += shares;
                        }
                    }
                }


                if (shareholderData.TotalShare > 0)
                {
                    shareholderData.SuperPct = Math.Round((decimal)shareholderData.SuperShare / shareholderData.TotalShare * 100, 2);
                    shareholderData.BigPct = Math.Round((decimal)shareholderData.BigShare / shareholderData.TotalShare * 100, 2);
                    shareholderData.RetailPct = Math.Round((decimal)shareholderData.RetailShare / shareholderData.TotalShare * 100, 2);

                    resultList.Add(shareholderData);
                    Console.WriteLine($"解析成功！{stockId} 於 {dateStr} 大戶比例: {shareholderData.BigPct}%");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"爬蟲發生錯誤: {ex.Message}");
            }

            return resultList;
        }

        // 抓取單一股票的整月歷史股價
        public async Task<List<DailyPrice>> FetchPriceHistoryAsync(string stockId, DateTime date)
        {
            var dateStr = date.ToString("yyyyMM01");
            return await FetchAndParseAsync<DailyPrice>(
                url: $"https://www.twse.com.tw/rwd/zh/afterTrading/STOCK_DAY?date={dateStr}&stockNo={stockId}&response=json",
                dataPath: "data",
                parseItem: row =>
                {
                    // 解析日期（民國年 → 西元年）
                    var rocDate = row[0].GetString();
                    var parts = rocDate.Split('/');
                    var year = int.Parse(parts[0]) + 1911;
                    var month = int.Parse(parts[1]);
                    var day = int.Parse(parts[2]);
                    var westernDate = new DateTime(year, month, day);

                    // 組出 DailyPrice
                    return new DailyPrice
                    {
                        StockId = stockId,          // 用參數傳進來的 stockId
                        Date = westernDate,
                        Open = ParseDecimal(row[3].GetString()),
                        High = ParseDecimal(row[4].GetString()),
                        Low = ParseDecimal(row[5].GetString()),
                        Close = ParseDecimal(row[6].GetString()),
                        Volume = ParseLong(row[1].GetString())
                    };
                },
                itemTypeName: "歷史股價"
            );
        }

        // 抓取全市場單日收盤行情 (MI_INDEX)
        public async Task<List<DailyPrice>> FetchMarketPricesByDateAsync(DateTime date, HashSet<string>? allowedStockIds = null)
        {
            var dateStr = date.ToString("yyyyMMdd");
            var url = $"https://www.twse.com.tw/exchangeReport/MI_INDEX?response=json&date={dateStr}&type=ALL";
            var resultList = new List<DailyPrice>();

            Console.WriteLine($"[爬蟲] 正在查詢 {dateStr} 全市場收盤行情...");
            try
            {
                var json = await _http.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("stat", out var stat) && stat.GetString() == "OK")
                {
                    if (root.TryGetProperty("tables", out var tables))
                    {
                        foreach (var table in tables.EnumerateArray())
                        {
                            if (table.TryGetProperty("title", out var titleProp) && titleProp.GetString()?.Contains("每日收盤行情(全部)") == true)
                            {
                                if (table.TryGetProperty("data", out var data))
                                {
                                    foreach (var row in data.EnumerateArray())
                                    {
                                        var stockId = row[0].GetString()?.Trim();
                                        if (string.IsNullOrEmpty(stockId)) continue;
                                        if (allowedStockIds != null && !allowedStockIds.Contains(stockId)) continue;

                                        try
                                        {
                                            resultList.Add(new DailyPrice
                                            {
                                                StockId = stockId,
                                                Date = date,
                                                Volume = ParseLong(row[2].GetString()), // 成交股數
                                                Open = ParseDecimal(row[5].GetString()),
                                                High = ParseDecimal(row[6].GetString()),
                                                Low = ParseDecimal(row[7].GetString()),
                                                Close = ParseDecimal(row[8].GetString())
                                            });
                                        }
                                        catch { }
                                    }
                                }
                                break; // 找到對應表格就結束
                            }
                        }
                    }
                }
                else
                {
                    var statStr = root.TryGetProperty("stat", out var s) ? s.GetString() ?? "" : "";
                    if (statStr.Contains("沒有") || statStr.Contains("無資料") || statStr.Contains("查無") || string.IsNullOrEmpty(statStr))
                        Console.WriteLine($"[提示] 歷史股價 (全市場) {dateStr} 無資料 (可能是假日或尚未結算)");
                    else
                        Console.WriteLine($"[錯誤] 取得 {dateStr} 收盤行情狀態異常: {statStr}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[錯誤] 取得 {dateStr} 收盤行情失敗: {ex.Message}");
            }
            return resultList;
        }

        // 抓取全市場 融資融券 (MI_MARGN)
        public async Task<List<Margins>> FetchMarginTradingAsync(DateTime date)
        {
            var todayStr = date.ToString("yyyyMMdd");
            var url = $"https://www.twse.com.tw/rwd/zh/marginTrading/MI_MARGN?date={todayStr}&selectType=ALL&response=json";

            var json = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var list = new List<Margins>();

            if (root.TryGetProperty("stat", out var stat) && stat.GetString() == "OK")
            {
                // 注意：資料在 tables 陣列裡，通常是第 2 個 (Index 1) "融資融券彙總 (全部)"
                // 但為了保險，我們用迴圈找標題對的那個 Table
                if (root.TryGetProperty("tables", out var tables))
                {
                    JsonElement targetTable = default;
                    bool found = false;

                    foreach (var table in tables.EnumerateArray())
                    {
                        if (table.TryGetProperty("title", out var title) && title.GetString()?.Contains("融資融券彙總") == true)
                        {
                            targetTable = table;
                            found = true;
                            break;
                        }
                    }

                    if (found && targetTable.TryGetProperty("data", out var data))
                    {
                        foreach (var row in data.EnumerateArray())
                        {
                            // 欄位對應: 0=代號, 2=資買, 3=資賣, 4=資現償, 6=資餘額, 8=券買, 9=券賣, 10=券現償, 12=券餘額
                            var sid = row[0].GetString();

                            if (!IsValidStockId(sid)) continue;

                            list.Add(new Margins
                            {
                                StockId = sid,
                                Date = date,
                                MarginBuy = (int)ParseDecimal(row[2].GetString()),
                                MarginSell = (int)ParseDecimal(row[3].GetString()),
                                MarginCashRepay = (int)ParseDecimal(row[4].GetString()),
                                MarginBalance = (int)ParseDecimal(row[6].GetString()),
                                ShortBuy = (int)ParseDecimal(row[8].GetString()),
                                ShortSell = (int)ParseDecimal(row[9].GetString()),
                                ShortCashRepay = (int)ParseDecimal(row[10].GetString()),
                                ShortBalance = (int)ParseDecimal(row[12].GetString())
                            });
                        }
                    }
                }
            }
            else
            {
                var statStr = root.TryGetProperty("stat", out var s) ? s.GetString() ?? "" : "";
                if (statStr.Contains("沒有") || statStr.Contains("無資料") || statStr.Contains("查無") || string.IsNullOrEmpty(statStr))
                    Console.WriteLine($"[提示] 融資融券 {date:yyyy-MM-dd} 無資料 (可能是假日)");
                else
                    Console.WriteLine($"[錯誤] 融資融券 API 回應狀態異常: {statStr}");
            }

            Console.WriteLine($"已取得 {date:yyyy-MM-dd} 全市場融資融券資料，共 {list.Count} 筆");
            return list;
        }

        // 抓取全市場 本益比、殖利率、股價淨值比 (BWIBBU)
        public async Task<List<Valuation>> FetchValuationsAsync(DateTime date)
        {
            var dateStr = date.ToString("yyyyMMdd");
            return await FetchAndParseAsync<Valuation>(
                url: $"https://www.twse.com.tw/exchangeReport/BWIBBU_d?response=json&selectType=ALL&date={dateStr}",
                dataPath: "data",
                parseItem: row =>
                {
                    // 取得股票代號
                    var sid = row[0].GetString();
                    if (!IsValidStockId(sid))
                    {
                        return null;  // 過濾掉非股票的 row
                    }

                    return new Valuation
                    {
                        StockId = sid,
                        Date = date,
                        DividendYield = ParseDecimalNullable(row[3].GetString()), // 殖利率
                        PERatio = ParseDecimalNullable(row[5].GetString()),       // 本益比
                        PBRatio = ParseDecimalNullable(row[6].GetString())        // 股價淨值比
                    };
                },
                itemTypeName: "估值資料"
            );
        }

        // 抓取全市場 當日沖銷交易資料 (TWTB4U)
        public async Task<List<DayTrade>> FetchDayTradesAsync(DateTime date)
        {
            var dateStr = date.ToString("yyyyMMdd");
            try
            {
                Console.WriteLine("正在抓取 當沖交易資料...");
                var json = await _http.GetStringAsync(
                    $"https://www.twse.com.tw/exchangeReport/TWTB4U?response=json&selectType=All&date={dateStr}");
                var doc = JsonDocument.Parse(json);

                // 檢查狀態
                if (!doc.RootElement.TryGetProperty("stat", out var status)
                    || status.GetString() != "OK")
                {
                    var statStr = status.GetString() ?? "";
                    if (statStr.Contains("沒有") || statStr.Contains("無資料") || statStr.Contains("查無") || string.IsNullOrEmpty(statStr))
                        Console.WriteLine($"[提示] 當沖交易資料 {dateStr} 無資料 (可能是假日)");
                    else
                        Console.WriteLine($"當沖交易資料 API 回應狀態異常: {statStr}");

                    return new List<DayTrade>();
                }

                // TWTB4U 的資料可能是 tables[0].data (或是檢查 title 包含 "當日沖銷交易")
                var tables = doc.RootElement.GetProperty("tables");
                JsonElement? dataArray = null;
                foreach (var table in tables.EnumerateArray())
                {
                    if (table.TryGetProperty("data", out var d))
                    {
                        dataArray = d;
                        break;
                    }
                }

                if (dataArray == null || dataArray.Value.GetArrayLength() == 0)
                {
                    Console.WriteLine($"[提示] 當沖交易資料 {dateStr} 無資料 (可能是假日或無交易)");
                    return new List<DayTrade>();
                }

                var result = new List<DayTrade>();
                foreach (var row in dataArray.Value.EnumerateArray())
                {
                    var sid = row[0].GetString();
                    if (!IsValidStockId(sid)) continue;

                    // 確認欄位數量足夠，避免小計列或格式不同列造成 IndexOutOfRangeException
                    if (row.GetArrayLength() < 6) continue;

                    result.Add(new DayTrade
                    {
                        StockId = sid,
                        Date = date,
                        Volume = ParseLong(row[3].GetString()),    // 當沖成交股數
                        BuyAmount = ParseLong(row[4].GetString()), // 當沖買進金額
                        SellAmount = ParseLong(row[5].GetString()) // 當沖賣出金額
                    });
                }

                Console.WriteLine($"成功取得 {result.Count} 筆當沖交易資料");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"抓取當沖交易資料失敗: {ex.Message}");
                return new List<DayTrade>();
            }
        }

        // 抓取全市場 除權除息資料 (TWT48U)
        public async Task<List<Dividend>> FetchDividendsAsync(DateTime startDate, DateTime endDate)
        {
            var startStr = startDate.ToString("yyyyMMdd");
            var endStr = endDate.ToString("yyyyMMdd");
            return await FetchAndParseAsync<Dividend>(
                url: $"https://www.twse.com.tw/exchangeReport/TWT48U?response=json&strDate={startStr}&endDate={endStr}",
                dataPath: "data",
                parseItem: row =>
                {
                    // 欄位: 0=除權息日期, 1=股票代號, 3=除權息類別, 4=無償配股率, 7=現金股利
                    var sid = row[1].GetString();
                    if (!IsValidStockId(sid))
                    {
                        return null;
                    }

                    return new Dividend
                    {
                        StockId = sid,
                        ExDate = ParseTaiwanDate(row[0].GetString()),
                        Type = row[3].GetString(),
                        StockDividendRate = ParseDecimalNullable(row[4].GetString()) ?? 0,
                        CashDividend = ParseDecimalNullable(row[7].GetString()) ?? 0
                    };
                },
                itemTypeName: "除權息資料"
            );
        }

        public async Task<List<StockInfo>> FetchCompanyDetailsAsync()
        {
            // 產業代號對照表放在外面，只建一次
            var industryMap = new Dictionary<string, string>
            {
                { "01", "水泥工業" }, { "02", "食品工業" }, { "03", "塑膠工業" }, { "04", "紡織纖維" },
                { "05", "電機機械" }, { "06", "電器電纜" }, { "07", "化學生技醫療" }, { "08", "玻璃陶瓷" },
                { "09", "造紙工業" }, { "10", "鋼鐵工業" }, { "11", "橡膠工業" }, { "12", "汽車工業" },
                { "13", "電子工業" }, { "14", "建材營造" }, { "15", "航運業" }, { "16", "觀光餐旅" },
                { "17", "金融保險" }, { "18", "貿易百貨" }, { "19", "綜合" }, { "20", "其他" },
                { "21", "化學工業" }, { "22", "生技醫療業" }, { "23", "油電燃氣業" }, { "24", "半導體業" },
                { "25", "電腦及週邊設備業" }, { "26", "光電業" }, { "27", "通信網路業" }, { "28", "電子零組件業" },
                { "29", "電子通路業" }, { "30", "資訊服務業" }, { "31", "其他電子業" }, { "32", "文化創意業" },
                { "33", "農業科技" }, { "34", "電子商務" }, { "35", "綠能環保" }, { "36", "數位雲端" },
                { "37", "運動休閒" }, { "38", "居家生活" },
                { "80", "管理股票" }, { "91", "TDR" }, { "97", "社會企業" }, { "98", "農林漁牧" }
            };

            return await FetchOpenDataArrayAsync<StockInfo>(
                url: "https://openapi.twse.com.tw/v1/opendata/t187ap03_L",
                parseItem: row =>
                {
                    var sid = row.GetProperty("公司代號").GetString();
                    if (!IsValidStockId(sid))
                    {
                        return null;  // 過濾掉非股票
                    }

                    var indCode = row.GetProperty("產業別").GetString();
                    var industryName = industryMap.TryGetValue(indCode, out var name)
                        ? name
                        : $"其他({indCode})";

                    return new StockInfo
                    {
                        StockId = sid,
                        Name = row.GetProperty("公司簡稱").GetString(),
                        Industry = industryName
                    };
                },
                itemTypeName: "產業分類資料"
            );
        }

        public async Task<List<Financial>> FetchFinancialsAsync()
        {
            return await FetchOpenDataArrayAsync<Financial>(
                url: "https://openapi.twse.com.tw/v1/opendata/t187ap17_L",
                parseItem: row =>
                {
                    var stockId = row.GetProperty("公司代號").GetString();
                    if (!IsValidStockId(stockId)) return null;

                    var yearStr = row.GetProperty("年度").GetString();
                    var quarterStr = row.GetProperty("季別").GetString();

                    var revenueMillions = ParseDecimalNullable(row.GetProperty("營業收入(百萬元)").GetString());
                    var grossMargin = ParseDecimalNullable(row.GetProperty("毛利率(%)(營業毛利)/(營業收入)").GetString());
                    var operatingMargin = ParseDecimalNullable(row.GetProperty("營業利益率(%)(營業利益)/(營業收入)").GetString());
                    var netProfitMargin = ParseDecimalNullable(row.GetProperty("稅後純益率(%)(稅後純益)/(營業收入)").GetString());

                    // 1. 營收: 百萬 -> 元
                    decimal? revenue = revenueMillions.HasValue ? revenueMillions.Value * 1_000_000m : null;

                    // 2. 推算絕對金額
                    decimal? grossProfit = null;
                    decimal? operatingProfit = null;
                    decimal? netProfit = null;

                    if (revenue.HasValue)
                    {
                        if (grossMargin.HasValue) grossProfit = revenue.Value * (grossMargin.Value / 100m);
                        if (operatingMargin.HasValue) operatingProfit = revenue.Value * (operatingMargin.Value / 100m);
                        if (netProfitMargin.HasValue) netProfit = revenue.Value * (netProfitMargin.Value / 100m);
                    }

                    // 3. EPS 暫時為 null
                    decimal? eps = null;

                    // 4. 年份處理 (民國 -> 西元)
                    int year = int.Parse(yearStr) + 1911;
                    int quarter = int.Parse(quarterStr);

                    return new Financial
                    {
                        StockId = stockId,
                        Year = year,
                        Quarter = quarter,
                        Revenue = revenue,
                        GrossProfit = grossProfit,
                        OperatingProfit = operatingProfit,
                        NetProfit = netProfit,
                        EPS = eps,
                        GrossMargin = grossMargin,
                        OperatingMargin = operatingMargin,
                        NetProfitMargin = netProfitMargin
                    };
                },
                itemTypeName: "財務報表資料"
            );
        }


        // 輔助函式 (可處理 "-" 或 null)
        private decimal? ParseDecimalNullable(string? s)
        {
            if (string.IsNullOrWhiteSpace(s) || s == "-" || s == "0.00") return null;
            if (decimal.TryParse(s.Replace(",", ""), out var result))
            {
                return result;
            }
            return null;
        }

        // 輔助函式
        private decimal ParseDecimal(string? s)
        {
            if (string.IsNullOrWhiteSpace(s) || s == "--") return 0;
            return decimal.Parse(s.Replace(",", ""));
        }

        private DateTime ParseTaiwanDate(string? s)
        {
            // 格式: "115年02月02日"
            if (string.IsNullOrWhiteSpace(s)) return DateTime.MinValue;

            try
            {
                // 移除 "年", "月", "日" 並分割
                var parts = s.Replace("年", "/").Replace("月", "/").Replace("日", "").Split('/');

                // parts[0] = "115" (民國年), parts[1] = "02", parts[2] = "02"
                int rocYear = int.Parse(parts[0]);
                int month = int.Parse(parts[1]);
                int day = int.Parse(parts[2]);

                // 民國年 + 1911 = 西元年
                int westernYear = rocYear + 1911;

                return new DateTime(westernYear, month, day);
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        private long ParseLong(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0;
            return long.Parse(s.Replace(",", ""));
        }

        private int ParseInt(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0;
            return int.Parse(s.Replace(",", ""));
        }

        // 判斷是否為有效股票代號 (包含ETF，排除權證)
        private bool IsValidStockId(string? sid)
        {
            // 排除 null 或空白
            if (string.IsNullOrWhiteSpace(sid)) return false;
            // 只接受 4-5 碼 (股票/ETF)，排除 6 碼以上 (權證)
            if (sid.Length < 4 || sid.Length > 5) return false;
            // 第一個字必須是數字 (排除"合計"等)
            return char.IsDigit(sid[0]);
        }
    }
}