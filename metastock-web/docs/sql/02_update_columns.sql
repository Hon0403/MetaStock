-- =============================================
-- 修改既有資料表 & 新增欄位 (Migration)
-- =============================================

-- A. daily_prices 表格修改
-- 1. 新增欄位 (如果不存在)
ALTER TABLE daily_prices ADD COLUMN IF NOT EXISTS change DECIMAL(10,2);
ALTER TABLE daily_prices ADD COLUMN IF NOT EXISTS change_percent DECIMAL(5,2);

-- 2. 補上中文註解
COMMENT ON TABLE daily_prices IS '每日股價資料表';
COMMENT ON COLUMN daily_prices.stock_id IS '股票代號';
COMMENT ON COLUMN daily_prices.date IS '交易日期';
COMMENT ON COLUMN daily_prices.open IS '開盤價';
COMMENT ON COLUMN daily_prices.high IS '最高價';
COMMENT ON COLUMN daily_prices.low IS '最低價';
COMMENT ON COLUMN daily_prices.close IS '收盤價';
COMMENT ON COLUMN daily_prices.volume IS '成交量';
COMMENT ON COLUMN daily_prices.change IS '漲跌價差';
COMMENT ON COLUMN daily_prices.change_percent IS '漲跌幅 (%)';


-- B. institutional_trading 表格修改
-- 1. 新增欄位 (如果不存在)
ALTER TABLE institutional_trading ADD COLUMN IF NOT EXISTS total_buy INTEGER;

-- 2. 補上中文註解
COMMENT ON TABLE institutional_trading IS '三大法人買賣超資料表';
COMMENT ON COLUMN institutional_trading.stock_id IS '股票代號';
COMMENT ON COLUMN institutional_trading.date IS '交易日期';
COMMENT ON COLUMN institutional_trading.foreign_buy IS '外資買賣超 (張)';
COMMENT ON COLUMN institutional_trading.trust_buy IS '投信買賣超 (張)';
COMMENT ON COLUMN institutional_trading.dealer_buy IS '自營商買賣超 (張)';
COMMENT ON COLUMN institutional_trading.total_buy IS '三大法人合計買賣超 (張)';


-- C. stocks 表格修改
-- 1. 補上中文註解
COMMENT ON TABLE stocks IS '股票基本資料表';
COMMENT ON COLUMN stocks.stock_id IS '股票代號';
COMMENT ON COLUMN stocks.name IS '股票名稱';
COMMENT ON COLUMN stocks.market IS '市場別 (TSE/OTC)';
COMMENT ON COLUMN stocks.industry IS '產業類別';
