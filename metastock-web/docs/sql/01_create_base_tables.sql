-- =============================================
-- MetaStock 基礎資料表 (含中文註解)
-- 在 Supabase SQL Editor 執行
-- =============================================

-- 1. stocks（股票基本資料）
CREATE TABLE stocks (
  stock_id VARCHAR(10) PRIMARY KEY,      -- 股票代號 (如: 2330)
  name VARCHAR(50),                      -- 股票名稱 (如: 台積電)
  market VARCHAR(10),                    -- 市場別 (TSE:上市, OTC:上櫃)
  industry VARCHAR(50),                  -- 產業類別 (如: 半導體業)
  created_at TIMESTAMP DEFAULT NOW()     -- 建立時間
);

-- 加入中文註解
COMMENT ON TABLE stocks IS '股票基本資料表';
COMMENT ON COLUMN stocks.stock_id IS '股票代號';
COMMENT ON COLUMN stocks.name IS '股票名稱';
COMMENT ON COLUMN stocks.market IS '市場別 (TSE/OTC)';
COMMENT ON COLUMN stocks.industry IS '產業類別';

-- 2. daily_prices（每日股價）
CREATE TABLE daily_prices (
  id SERIAL PRIMARY KEY,
  stock_id VARCHAR(10) REFERENCES stocks(stock_id), -- 關聯到股票基本資料
  date DATE NOT NULL,                               -- 交易日期
  open DECIMAL(10,2),                               -- 開盤價
  high DECIMAL(10,2),                               -- 最高價
  low DECIMAL(10,2),                                -- 最低價
  close DECIMAL(10,2),                              -- 收盤價
  volume BIGINT,                                    -- 成交量 (股)
  change DECIMAL(10,2),                             -- 漲跌價差 (新增: 更直觀)
  change_percent DECIMAL(5,2),                      -- 漲跌幅 % (新增: 更直觀)
  created_at TIMESTAMP DEFAULT NOW(),
  UNIQUE(stock_id, date)                            -- 確保同一支股票同一天只有一筆資料
);

-- 加入中文註解
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

-- 股價查詢索引
CREATE INDEX idx_daily_prices_stock_date ON daily_prices(stock_id, date DESC);

-- 3. institutional_trading（法人買賣超）
CREATE TABLE institutional_trading (
  id SERIAL PRIMARY KEY,
  stock_id VARCHAR(10) REFERENCES stocks(stock_id),
  date DATE NOT NULL,                               -- 交易日期
  foreign_buy INTEGER,                              -- 外資買賣超張數
  trust_buy INTEGER,                                -- 投信買賣超張數
  dealer_buy INTEGER,                               -- 自營商買賣超張數
  total_buy INTEGER,                                -- 三大法人合計 (新增: 方便查詢)
  created_at TIMESTAMP DEFAULT NOW(),
  UNIQUE(stock_id, date)
);

-- 加入中文註解
COMMENT ON TABLE institutional_trading IS '三大法人買賣超資料表';
COMMENT ON COLUMN institutional_trading.stock_id IS '股票代號';
COMMENT ON COLUMN institutional_trading.date IS '交易日期';
COMMENT ON COLUMN institutional_trading.foreign_buy IS '外資買賣超 (張)';
COMMENT ON COLUMN institutional_trading.trust_buy IS '投信買賣超 (張)';
COMMENT ON COLUMN institutional_trading.dealer_buy IS '自營商買賣超 (張)';
COMMENT ON COLUMN institutional_trading.total_buy IS '三大法人合計買賣超 (張)';

-- 法人資料查詢索引
CREATE INDEX idx_institutional_stock_date ON institutional_trading(stock_id, date DESC);

