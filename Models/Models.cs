using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MetaStock.Models
{
    // TWSE API 回應模型
    public class TwseApiResponse
    {
        [JsonPropertyName("stat")]
        public string Status { get; set; }

        [JsonPropertyName("date")]
        public string Date { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("fields")]
        public List<string> Fields { get; set; }

        [JsonPropertyName("data")]
        public List<List<string>> Data { get; set; }
    }

    // 股票資料模型
    public class StockData
    {
        public string Date { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public long Volume { get; set; }
        public decimal Change { get; set; }
        public string ChangePercent { get; set; }
        public string PriceChangeClass { get; set; }
    }

    // 股票基本資訊模型
    public class StockInfo
    {
        public string StockId { get; set; }
        public string StockName { get; set; }
        public decimal CurrentPrice { get; set; }
        public decimal OpenPrice { get; set; }
        public decimal HighPrice { get; set; }
        public decimal LowPrice { get; set; }
        public decimal PrevClose { get; set; }
        public decimal Change { get; set; }
        public string ChangePercent { get; set; }
        public long Volume { get; set; }
        public decimal? PE { get; set; }
        public string PriceChangeClass { get; set; }
    }

    // K線資料模型
    public class KLineData
    {
        public List<string> Dates { get; set; } = new List<string>();
        public List<decimal[]> KlineData { get; set; } = new List<decimal[]>();
        public List<long> Volumes { get; set; } = new List<long>();
    }
}