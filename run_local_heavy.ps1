# run_local_heavy.ps1
# 專門用來在台灣本地電腦執行「耗時極長、需要台灣IP」的 API 爬蟲腳本
# 包含：券商分點買賣日報 (Brokertrades) 和 集保戶股權分散 (Shareholders)

# 請在這裡輸入你的 Supabase 資料庫連線字串 (務必包含密碼)
$env:PG_CONNECTION="Host=158.101.149.77;Port=5432;Username=metastock;Password=MetaStock2026!;Database=metastock"

# 設定要回溯最近幾個禮拜的資料
$WEEKS_TO_FETCH = 1

Write-Host "=================================================" -ForegroundColor Cyan
Write-Host " 啟動 MetaStock 本地端專用爬蟲 (全市場券商分點 & 集保戶)" -ForegroundColor Cyan
Write-Host " ※ 警告: 抓取全市場約需 2~4 小時，請保持電腦甦醒" -ForegroundColor Yellow
Write-Host "=================================================" -ForegroundColor Cyan
Write-Host ""

Write-Host ">> 正在抓取全市場資料..." -ForegroundColor Green

# 執行 dotnet run，不帶 --stock 代表抓取全市場
# 本腳本專注於跑本機需要的重型 API (Action 已經跑掉輕量的了)
dotnet run --project metastock-sync/metastock-sync.csproj --weeks $WEEKS_TO_FETCH

Write-Host ""
Write-Host "=================================================" -ForegroundColor Cyan
Write-Host " 全市場本地端專用爬蟲執行完畢！" -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan
