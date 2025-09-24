using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text.Json;

namespace MetaStock.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class StockBasicInfo : ControllerBase
    {
        private readonly HttpClient _httpClient;

        public StockBasicInfo(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient();
        }

        // 取得股票基本資訊（收盤後資料）
        [HttpGet]
        public async Task<IActionResult> GetBasicInfo(string stockId)
        {
            try
            {
                var prefix = stockId.StartsWith("6") ? "otc" : "tse";
                var url = $"https://mis.twse.com.tw/stock/api/getStockInfo.jsp?ex_ch={prefix}_{stockId}.tw&json=1";

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

                var response = await _httpClient.GetStringAsync(url);
                return Content(response, "application/json");
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    msgArray = new object[0],
                    rtcode = "5000",
                    rtmessage = ex.Message
                });
            }
        }
    }
}
