using System.Text.Json.Serialization;

namespace MetaStockSync
{
    // 對應 stocks 表
    public class StockInfo
    {
        [JsonPropertyName("stock_id")]
        public string StockId { get; set; }

        [JsonPropertyName("industry")]
        public string Industry { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("market")]
        public string Market { get; set; } = "TSE";
    }

    // 對應 prices 表
    public class DailyPrice
    {
        [JsonPropertyName("stock_id")]
        public string StockId { get; set; }

        [JsonPropertyName("date")]
        public DateTime Date { get; set; }

        [JsonPropertyName("open")]
        public decimal Open { get; set; }

        [JsonPropertyName("high")]
        public decimal High { get; set; }

        [JsonPropertyName("low")]
        public decimal Low { get; set; }

        [JsonPropertyName("close")]
        public decimal Close { get; set; }

        [JsonPropertyName("volume")]
        public long Volume { get; set; }

        [JsonPropertyName("change")]
        public decimal Change { get; set; }

        [JsonPropertyName("change_pct")]
        public decimal ChangePct { get; set; }
    }

    // 對應 institutional 表
    public class InstitutionalTrade
    {
        [JsonPropertyName("stock_id")]
        public string StockId { get; set; }

        [JsonPropertyName("date")]
        public DateTime Date { get; set; }

        [JsonPropertyName("foreign_net")]
        public int ForeignNet { get; set; }

        [JsonPropertyName("trust_net")]
        public int TrustNet { get; set; }

        [JsonPropertyName("dealer_net")]
        public int DealerNet { get; set; }

        [JsonPropertyName("total_net")]
        public int TotalNet { get; set; }
    }

    // 對應 revenues 表
    public class MonthlyRevenue
    {
        [JsonPropertyName("stock_id")]
        public string StockId { get; set; }

        [JsonPropertyName("year")]
        public int Year { get; set; }

        [JsonPropertyName("month")]
        public int Month { get; set; }

        [JsonPropertyName("date")]
        public DateTime Date { get; set; }

        [JsonPropertyName("revenue")]
        public decimal Revenue { get; set; }

        [JsonPropertyName("mom_pct")]
        public decimal MomPct { get; set; }

        [JsonPropertyName("yoy_pct")]
        public decimal YoyPct { get; set; }
    }

    // 對應 shareholders 表
    public class Shareholders
    {
        [JsonPropertyName("stock_id")]
        public string StockId { get; set; }

        [JsonPropertyName("date")]
        public DateTime Date { get; set; }

        [JsonPropertyName("super_share")]
        public long SuperShare { get; set; }

        [JsonPropertyName("big_share")]
        public long BigShare { get; set; }

        [JsonPropertyName("retail_share")]
        public long RetailShare { get; set; }

        [JsonPropertyName("total_share")]
        public long TotalShare { get; set; }

        [JsonPropertyName("super_pct")]
        public decimal SuperPct { get; set; }

        [JsonPropertyName("big_pct")]
        public decimal BigPct { get; set; }

        [JsonPropertyName("retail_pct")]
        public decimal RetailPct { get; set; }
    }

    // 對應 margins 表
    public class Margins
    {
        [JsonPropertyName("stock_id")]
        public string StockId { get; set; }

        [JsonPropertyName("date")]
        public DateTime Date { get; set; }

        [JsonPropertyName("margin_buy")]
        public int MarginBuy { get; set; }

        [JsonPropertyName("margin_sell")]
        public int MarginSell { get; set; }

        [JsonPropertyName("margin_cash_repay")]
        public int MarginCashRepay { get; set; }

        [JsonPropertyName("margin_balance")]
        public int MarginBalance { get; set; }

        [JsonPropertyName("short_buy")]
        public int ShortBuy { get; set; }

        [JsonPropertyName("short_sell")]
        public int ShortSell { get; set; }

        [JsonPropertyName("short_cash_repay")]
        public int ShortCashRepay { get; set; }

        [JsonPropertyName("short_balance")]
        public int ShortBalance { get; set; }
    }

    // 對應 valuations 表
    public class Valuation
    {
        [JsonPropertyName("stock_id")]
        public string StockId { get; set; }

        [JsonPropertyName("date")]
        public DateTime Date { get; set; }

        [JsonPropertyName("pe_ratio")]
        public decimal? PERatio { get; set; } // 本益比

        [JsonPropertyName("dividend_yield")]
        public decimal? DividendYield { get; set; } // 殖利率

        [JsonPropertyName("pb_ratio")]
        public decimal? PBRatio { get; set; } // 股價淨值比
    }

    // 對應 daytrades 表
    public class DayTrade
    {
        [JsonPropertyName("stock_id")]
        public string StockId { get; set; }

        [JsonPropertyName("date")]
        public DateTime Date { get; set; }

        [JsonPropertyName("volume")]
        public long Volume { get; set; } // 當沖成交股數

        [JsonPropertyName("buy_amount")]
        public long BuyAmount { get; set; } // 當沖買進金額

        [JsonPropertyName("sell_amount")]
        public long SellAmount { get; set; } // 當沖賣出金額
    }

    // 對應 dividends 表
    public class Dividend
    {
        [JsonPropertyName("stock_id")]
        public string StockId { get; set; }

        [JsonPropertyName("ex_date")]
        public DateTime ExDate { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; } // '權' 或 '息'

        [JsonPropertyName("cash_dividend")]
        public decimal CashDividend { get; set; } // 現金股利 (元/股)

        [JsonPropertyName("stock_dividend_rate")]
        public decimal StockDividendRate { get; set; } // 無償配股率
    }

    // 對應 brokertrades 表
    public class BrokerTrade
    {
        [JsonPropertyName("stock_id")]
        public string StockId { get; set; }

        [JsonPropertyName("date")]
        public DateTime Date { get; set; }

        [JsonPropertyName("broker_id")]
        public string BrokerId { get; set; }

        [JsonPropertyName("broker_name")]
        public string BrokerName { get; set; }

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [JsonPropertyName("buy_volume")]
        public int BuyVolume { get; set; }

        [JsonPropertyName("sell_volume")]
        public int SellVolume { get; set; }
    }

    // 對應 financials 表
    public class Financial
    {
        [JsonPropertyName("stock_id")]
        public string StockId { get; set; }

        [JsonPropertyName("year")]
        public int Year { get; set; }

        [JsonPropertyName("quarter")]
        public int Quarter { get; set; }

        [JsonPropertyName("revenue")]
        public decimal? Revenue { get; set; } // 營業收入

        [JsonPropertyName("gross_profit")]
        public decimal? GrossProfit { get; set; } // 營業毛利

        [JsonPropertyName("operating_profit")]
        public decimal? OperatingProfit { get; set; } // 營業利益

        [JsonPropertyName("net_profit")]
        public decimal? NetProfit { get; set; } // 本期淨利

        [JsonPropertyName("eps")]
        public decimal? EPS { get; set; } // 每股盈餘

        [JsonPropertyName("gross_margin")]
        public decimal? GrossMargin { get; set; } // 毛利率

        [JsonPropertyName("operating_margin")]
        public decimal? OperatingMargin { get; set; } // 營益率

        [JsonPropertyName("net_profit_margin")]
        public decimal? NetProfitMargin { get; set; } // 淨利率
    }
}
