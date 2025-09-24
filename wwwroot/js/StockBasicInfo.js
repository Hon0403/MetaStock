// 頁面載入時自動載入基本資訊
$(document).ready(function () {
    refreshBasicInfo();
});

// 對應的前端呼叫
function refreshBasicInfo() {
    $.ajax({
        url: '/api/StockBasicInfo',
        data: { stockId: currentStockId },
        type: 'GET',
        success: function (data) {
            updateBasicInfoDisplay(data);
        },
        error: function () {
            console.log('無法載入基本資料');
        }
    });
}

function updateBasicInfoDisplay(data) {
    if (data && data.msgArray && data.msgArray[0]) {
        const stock = data.msgArray[0];

        document.getElementById('stockId').textContent = stock.c;
        document.getElementById('stockName').textContent = stock.n;

        // 使用共用格式化函數
        document.getElementById('closePrice').textContent = `$${StockFormatter.formatPrice(stock.z)}`;
        document.getElementById('openPrice').textContent = `$${StockFormatter.formatPrice(stock.o)}`;
        document.getElementById('highPrice').textContent = `$${StockFormatter.formatPrice(stock.h)}`;
        document.getElementById('lowPrice').textContent = `$${StockFormatter.formatPrice(stock.l)}`;
        document.getElementById('prevClose').textContent = `$${StockFormatter.formatPrice(stock.y)}`;

        // 使用共用漲跌計算
        const changeInfo = StockFormatter.calculateChange(stock.z, stock.y);
        document.getElementById('dailyChange').innerHTML =
            `<span class="${changeInfo.className}">${changeInfo.text}</span>`;

        document.getElementById('volume').textContent = StockFormatter.formatVolume(stock.v);
        document.getElementById('stockTitle').textContent = `${stock.n}(${stock.c})`;
    }
}