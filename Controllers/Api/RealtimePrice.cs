using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text.Json;

namespace MetaStock.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class RealtimePrice : ControllerBase
    {
        private readonly HttpClient _httpClient;

        public RealtimePrice(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient();
        }

        // 即時股價API
        [HttpGet]
        public async Task<IActionResult> GetRealtimePrice(string stockId)
        {
            try
            {
                var url = $"https://mis.twse.com.tw/stock/api/getStockInfo.jsp?ex_ch=tse_{stockId}.tw";

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
