# run_local_heavy.ps1
# 專門用來在台灣本地電腦執行「耗時極長、需要台灣IP」的 API 爬蟲腳本
# 包含：券商分點買賣日報 (Brokertrades) 和 集保戶股權分散 (Shareholders)

# 請在這裡輸入你的 Supabase 資料庫連線字串 (務必包含密碼)
$env:PG_CONNECTION="Host=192.168.1.xxxx;Username=postgres;Password=YOUR_PASSWORD;Database=postgres"

# 在這裡設定你想抓取的目標「自選股」清單 (請用半形逗號分隔)
# 例如: $TARGET_STOCKS = @("2330", "0050", "2603")
$TARGET_STOCKS = @("2330")

# 設定要回溯最近幾個禮拜的資料
$WEEKS_TO_FETCH = 1

Write-Host "=================================================" -ForegroundColor Cyan
Write-Host " 啟動 MetaStock 本地端專用爬蟲 (券商分點 & 集保戶)" -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan
Write-Host ""

foreach ($stock in $TARGET_STOCKS) {
    Write-Host ">> 正在抓取自選股: $stock ..." -ForegroundColor Green
    
    # 執行 dotnet run，並且用 --skip-fast 來略過那些已經在 GitHub Action 跑過的 API
    # 由於我們沒有實作 --skip-fast 參數，我們直接利用預設行為：
    # 因為這個腳本是在平日下午盤後跑的，只要 Action 已經把大盤寫進去了，
    # ExistsAsync 就會自動跳過那些大盤資料，所以不會重複花費時間。
    
    dotnet run --project metastock-sync/metastock-sync.csproj --stock $stock --weeks $WEEKS_TO_FETCH
    
    Write-Host ">> $stock 抓取完畢。休息 10 秒鐘避免遭到證交所阻擋..." -ForegroundColor Yellow
    Start-Sleep -Seconds 10
}

Write-Host ""
Write-Host "=================================================" -ForegroundColor Cyan
Write-Host " 所有自選股的本地端專用爬蟲執行完畢！" -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan
