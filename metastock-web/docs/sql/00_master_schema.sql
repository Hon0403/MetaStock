-- ============================================================================
-- MetaStock Database Master Schema
-- Generated at: 2026-01-29
-- Description: 
--   包含目前所有系統用到的 Table 定義。
--   使用 Natural Key (e.g. stock_id + date) 作為 Primary Key，不使用流水號 ID。
-- ============================================================================

-- ----------------------------------------------------------------------------
-- 1. 股票基本資料 (Stocks)
-- Source API: STOCK_DAY_ALL
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS stocks (
    stock_id TEXT PRIMARY KEY,
    industry TEXT,
    name TEXT NOT NULL,
    market TEXT DEFAULT 'TSE',
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT timezone('utc'::text, now())
);

COMMENT ON TABLE stocks IS '股票基本資料表';
COMMENT ON COLUMN stocks.stock_id IS '股票代號 (如 2330)';
COMMENT ON COLUMN stocks.name IS '股票名稱 (如 台積電)';
COMMENT ON COLUMN stocks.market IS '市場別 (TSE=上市, OTC=上櫃)';
COMMENT ON COLUMN stocks.updated_at IS '最後更新時間';

-- ----------------------------------------------------------------------------
-- 2. 每日股價 (Daily Prices)
-- Source API: STOCK_DAY
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS prices (
    stock_id TEXT NOT NULL,
    date DATE NOT NULL,
    open NUMERIC,
    high NUMERIC,
    low NUMERIC,
    close NUMERIC,
    volume BIGINT,
    change NUMERIC,
    change_pct NUMERIC,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT timezone('utc'::text, now()),
    PRIMARY KEY (stock_id, date)
);

COMMENT ON TABLE prices IS '每日股價行情';
COMMENT ON COLUMN prices.stock_id IS '股票代號';
COMMENT ON COLUMN prices.date IS '交易日期';
COMMENT ON COLUMN prices.open IS '開盤價';
COMMENT ON COLUMN prices.high IS '最高價';
COMMENT ON COLUMN prices.low IS '最低價';
COMMENT ON COLUMN prices.close IS '收盤價';
COMMENT ON COLUMN prices.volume IS '成交股數';
COMMENT ON COLUMN prices.change IS '漲跌價差';
COMMENT ON COLUMN prices.change_pct IS '漲跌幅 (%)';

-- ----------------------------------------------------------------------------
-- 3. 三大法人買賣超 (Institutional Trades)
-- Source API: T86
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS institutional (
    stock_id TEXT NOT NULL,
    date DATE NOT NULL,
    foreign_net INTEGER DEFAULT 0,
    trust_net INTEGER DEFAULT 0,
    dealer_net INTEGER DEFAULT 0,
    total_net INTEGER DEFAULT 0,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT timezone('utc'::text, now()),
    PRIMARY KEY (stock_id, date)
);

COMMENT ON TABLE institutional IS '三大法人每日買賣超';
COMMENT ON COLUMN institutional.foreign_net IS '外資買賣超張數';
COMMENT ON COLUMN institutional.trust_net IS '投信買賣超張數';
COMMENT ON COLUMN institutional.dealer_net IS '自營商買賣超張數';
COMMENT ON COLUMN institutional.total_net IS '三大法人合計買賣超';

-- ----------------------------------------------------------------------------
-- 4. 每月營收 (Monthly Revenues)
-- Source API: MOPS OpenData / HTML
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS revenues (
    stock_id TEXT NOT NULL,
    year INTEGER NOT NULL,
    month INTEGER NOT NULL,
    date DATE NOT NULL,
    revenue NUMERIC,
    mom_pct NUMERIC,
    yoy_pct NUMERIC,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT timezone('utc'::text, now()),
    PRIMARY KEY (stock_id, year, month)
);

COMMENT ON TABLE revenues IS '每月營收申報';
COMMENT ON COLUMN revenues.stock_id IS '股票代號';
COMMENT ON COLUMN revenues.year IS '西元年';
COMMENT ON COLUMN revenues.month IS '月份';
COMMENT ON COLUMN revenues.date IS '資料日期 (通常為該月1日)';
COMMENT ON COLUMN revenues.revenue IS '當月營收 (千元)';
COMMENT ON COLUMN revenues.mom_pct IS '月增率 (%)';
COMMENT ON COLUMN revenues.yoy_pct IS '年增率 (%)';

-- ----------------------------------------------------------------------------
-- 5. 集保戶股權分散 (Shareholders)
-- Source API: TDCC
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS shareholders (
    stock_id TEXT NOT NULL,
    date DATE NOT NULL,
    super_share BIGINT,
    big_share BIGINT,
    retail_share BIGINT,
    total_share BIGINT,
    super_pct NUMERIC,
    big_pct NUMERIC,
    retail_pct NUMERIC,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT timezone('utc'::text, now()),
    PRIMARY KEY (stock_id, date)
);

COMMENT ON TABLE shareholders IS '集保戶股權分散表 (大戶籌碼)';
COMMENT ON COLUMN shareholders.date IS '資料發布日期 (通常為週五)';
COMMENT ON COLUMN shareholders.super_share IS '超級大戶持股數 (>1000張)';
COMMENT ON COLUMN shareholders.big_share IS '大戶持股數 (>400張)';
COMMENT ON COLUMN shareholders.retail_share IS '散戶持股數 (<50張)';
COMMENT ON COLUMN shareholders.total_share IS '總發行股數';
COMMENT ON COLUMN shareholders.super_pct IS '超級大戶持股比例 (%)';
COMMENT ON COLUMN shareholders.big_pct IS '大戶持股比例 (%)';
COMMENT ON COLUMN shareholders.retail_pct IS '散戶持股比例 (%)';

-- ----------------------------------------------------------------------------
-- 6. 融資融券餘額 (Margin Trading)
-- Source API: MI_MARGN
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS margins (
    stock_id TEXT NOT NULL,
    date DATE NOT NULL,
    
    -- 融資
    margin_buy INTEGER DEFAULT 0,
    margin_sell INTEGER DEFAULT 0,
    margin_cash_repay INTEGER DEFAULT 0,
    margin_balance INTEGER DEFAULT 0,
    
    -- 融券
    short_buy INTEGER DEFAULT 0,
    short_sell INTEGER DEFAULT 0,
    short_cash_repay INTEGER DEFAULT 0,
    short_balance INTEGER DEFAULT 0,
    
    created_at TIMESTAMP WITH TIME ZONE DEFAULT timezone('utc'::text, now()),
    PRIMARY KEY (stock_id, date)
);

COMMENT ON TABLE margins IS '融資融券餘額表';
COMMENT ON COLUMN margins.margin_buy IS '融資買進張數';
COMMENT ON COLUMN margins.margin_sell IS '融資賣出張數';
COMMENT ON COLUMN margins.margin_cash_repay IS '融資現金償還';
COMMENT ON COLUMN margins.margin_balance IS '融資今日餘額';
COMMENT ON COLUMN margins.short_buy IS '融券買進 (回補) 張數';
COMMENT ON COLUMN margins.short_sell IS '融券賣出張數';
COMMENT ON COLUMN margins.short_cash_repay IS '融券現金償還';
COMMENT ON COLUMN margins.short_balance IS '融券今日餘額';

-- ----------------------------------------------------------------------------
-- 7. 市場估值指標 (Valuations)
-- Source API: BWIBBU (本益比、殖利率、股價淨值比)
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS valuations (
    stock_id TEXT NOT NULL,
    date DATE NOT NULL,
    
    pe_ratio NUMERIC,        -- 本益比 (P/E Ratio)
    dividend_yield NUMERIC,  -- 殖利率 (Dividend Yield %)
    pb_ratio NUMERIC,        -- 股價淨值比 (P/B Ratio)
    
    created_at TIMESTAMP WITH TIME ZONE DEFAULT timezone('utc'::text, now()),
    PRIMARY KEY (stock_id, date)
);

COMMENT ON TABLE valuations IS '市場估值指標 (本益比/殖利率/淨值比)';
COMMENT ON COLUMN valuations.stock_id IS '股票代號';
COMMENT ON COLUMN valuations.date IS '交易日期';
COMMENT ON COLUMN valuations.pe_ratio IS '本益比 (P/E)';
COMMENT ON COLUMN valuations.dividend_yield IS '殖利率 (%)';
COMMENT ON COLUMN valuations.pb_ratio IS '股價淨值比 (P/B)';

-- ----------------------------------------------------------------------------
-- 8. [NEW] 當日沖銷交易 (Day Trading)
-- Source API: TWTB4U
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS daytrades (
    stock_id TEXT NOT NULL,
    date DATE NOT NULL,
    
    volume BIGINT DEFAULT 0,       -- 當沖成交股數
    buy_amount NUMERIC DEFAULT 0,  -- 當沖買進金額
    sell_amount NUMERIC DEFAULT 0, -- 當沖賣出金額
    
    created_at TIMESTAMP WITH TIME ZONE DEFAULT timezone('utc'::text, now()),
    PRIMARY KEY (stock_id, date)
);

COMMENT ON TABLE daytrades IS '當日沖銷交易';
COMMENT ON COLUMN daytrades.volume IS '當沖成交股數';
COMMENT ON COLUMN daytrades.buy_amount IS '當沖買進金額';
COMMENT ON COLUMN daytrades.sell_amount IS '當沖賣出金額';

-- ----------------------------------------------------------------------------
-- 9. [NEW] 除權除息資料 (Dividends)
-- Source API: TWT48U (除權除息預告表)
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS dividends (
    stock_id TEXT NOT NULL,
    ex_date DATE NOT NULL,           -- 除權息日期
    
    type TEXT,                       -- '權' 或 '息'
    cash_dividend NUMERIC,           -- 現金股利 (元/股)
    stock_dividend_rate NUMERIC,     -- 無償配股率
    
    created_at TIMESTAMP WITH TIME ZONE DEFAULT timezone('utc'::text, now()),
    PRIMARY KEY (stock_id, ex_date)
);

COMMENT ON TABLE dividends IS '除權除息資料';
COMMENT ON COLUMN dividends.ex_date IS '除權息日期';
COMMENT ON COLUMN dividends.type IS '權=除權, 息=除息';
COMMENT ON COLUMN dividends.cash_dividend IS '現金股利 (元/股)';
COMMENT ON COLUMN dividends.stock_dividend_rate IS '無償配股率';

-- ----------------------------------------------------------------------------
-- 10. [NEW] 券商分點基本資料 (Brokers)
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS brokers (
    broker_id TEXT PRIMARY KEY,      -- 券商代號 (如 1470)
    broker_name TEXT,                -- 券商名稱 (如 台灣摩根士丹利)
    type TEXT,                       -- 類型: '外資' / '本土'
    created_at TIMESTAMP WITH TIME ZONE DEFAULT timezone('utc'::text, now())
);

COMMENT ON TABLE brokers IS '券商分點基本資料';

-- ----------------------------------------------------------------------------
-- 11. [NEW] 券商分點每日進出 (Brokertrades)
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS brokertrades (
    stock_id TEXT NOT NULL,
    date DATE NOT NULL,
    broker_id TEXT NOT NULL,
    broker_name TEXT NOT NULL,       -- [NEW] 券商名稱
    price NUMERIC NOT NULL,          -- [NEW] 成交價格
    
    buy_volume INTEGER NOT NULL,     -- [RENAME] buy_qty -> buy_volume
    sell_volume INTEGER NOT NULL,    -- [RENAME] sell_qty -> sell_volume
    
    created_at TIMESTAMP WITH TIME ZONE DEFAULT timezone('utc'::text, now()) NOT NULL,
    
    -- Composite Primary Key 必須包含 price，因為同一天同券商會有不同價格的成交
    PRIMARY KEY (stock_id, date, broker_id, price)
);

COMMENT ON TABLE brokertrades IS '券商分點買賣日報表';
COMMENT ON COLUMN brokertrades.stock_id IS '股票代號';
COMMENT ON COLUMN brokertrades.date IS '交易日期';
COMMENT ON COLUMN brokertrades.broker_id IS '券商代號 (4碼)';
COMMENT ON COLUMN brokertrades.broker_name IS '券商名稱';
COMMENT ON COLUMN brokertrades.price IS '成交價格';
COMMENT ON COLUMN brokertrades.buy_volume IS '買進股數';
COMMENT ON COLUMN brokertrades.sell_volume IS '賣出股數';

-- ----------------------------------------------------------------------------
-- 12. [NEW] 財務報表 (Financial Statements)
-- Source API: t187ap17_L (綜合損益表)
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS financials (
    stock_id TEXT NOT NULL,
    year INTEGER NOT NULL,           -- 西元年 (e.g., 2024)
    quarter INTEGER NOT NULL,        -- 季度 (1, 2, 3, 4)
    
    revenue NUMERIC,                 -- 營業收入
    gross_profit NUMERIC,            -- 營業毛利
    operating_profit NUMERIC,        -- 營業利益
    net_profit NUMERIC,              -- 本期淨利 (歸屬於母公司業主)
    eps NUMERIC,                     -- 基本每股盈餘 (元)
    
    gross_margin NUMERIC,            -- 毛利率 (%)
    operating_margin NUMERIC,        -- 營益率 (%)
    net_profit_margin NUMERIC,       -- 淨利率 (%)
    
    created_at TIMESTAMP WITH TIME ZONE DEFAULT timezone('utc'::text, now()),
    PRIMARY KEY (stock_id, year, quarter)
);

COMMENT ON TABLE financials IS '財務報表 (季報)';
COMMENT ON COLUMN financials.stock_id IS '股票代號';
COMMENT ON COLUMN financials.year IS '年度';
COMMENT ON COLUMN financials.quarter IS '季度';
COMMENT ON COLUMN financials.revenue IS '營業收入';
COMMENT ON COLUMN financials.gross_profit IS '營業毛利';
COMMENT ON COLUMN financials.operating_profit IS '營業利益';
COMMENT ON COLUMN financials.net_profit IS '稅後淨利';
COMMENT ON COLUMN financials.eps IS '每股盈餘 (EPS)';
COMMENT ON COLUMN financials.gross_margin IS '毛利率 (%)';
COMMENT ON COLUMN financials.operating_margin IS '營益率 (%)';
COMMENT ON COLUMN financials.net_profit_margin IS '淨利率 (%)';
-- ----------------------------------------------------------------------------
-- 13. [NEW] 補充缺漏的欄位註解
-- ----------------------------------------------------------------------------
COMMENT ON COLUMN stocks.industry IS '產業別';
COMMENT ON COLUMN prices.created_at IS '建立時間';
COMMENT ON COLUMN institutional.stock_id IS '股票代號';
COMMENT ON COLUMN institutional.date IS '交易日期';
COMMENT ON COLUMN institutional.created_at IS '建立時間';
COMMENT ON COLUMN revenues.created_at IS '建立時間';
COMMENT ON COLUMN shareholders.stock_id IS '股票代號';
COMMENT ON COLUMN shareholders.created_at IS '建立時間';
COMMENT ON COLUMN margins.stock_id IS '股票代號';
COMMENT ON COLUMN margins.date IS '交易日期';
COMMENT ON COLUMN margins.created_at IS '建立時間';
COMMENT ON COLUMN valuations.created_at IS '建立時間';
COMMENT ON COLUMN daytrades.stock_id IS '股票代號';
COMMENT ON COLUMN daytrades.date IS '交易日期';
COMMENT ON COLUMN daytrades.created_at IS '建立時間';
COMMENT ON COLUMN dividends.stock_id IS '股票代號';
COMMENT ON COLUMN dividends.created_at IS '建立時間';
COMMENT ON COLUMN brokers.broker_id IS '券商代號';
COMMENT ON COLUMN brokers.broker_name IS '券商名稱';
COMMENT ON COLUMN brokers.type IS '券商類型';
COMMENT ON COLUMN brokers.created_at IS '建立時間';
COMMENT ON COLUMN brokertrades.created_at IS '建立時間';
COMMENT ON COLUMN financials.created_at IS '建立時間';
