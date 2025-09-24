var currentStockId;
var miniChart;
var miniKlineData = [];
var isTestMode = true;
// isTestMode設定false是對正式api丟請求,true是模擬資料測試及時股價的功能

// 統一解析證交所API格式
function parseStockData(apiResponse) {
    if (apiResponse.msgArray && apiResponse.msgArray.length > 0) {
        const stock = apiResponse.msgArray[0];
        return {
            currentPrice: stock.z || '0',
            yesterdayPrice: stock.y || '0',
            volume: stock.v || '0',
            openPrice: stock.o || '0',
            highPrice: stock.h || '0',
            lowPrice: stock.l || '0',
            stockId: stock.c || '',
            stockName: stock.n || '',
            updateTime: new Date().toLocaleTimeString()
        };
    }
    return apiResponse;
}

function getRealtimePriceAjax(stockId) {
    var apiUrl = isTestMode ? '/api/MockRealtimePrice' : '/api/RealtimePrice';

    $.ajax({
        url: apiUrl,
        data: { stockId: stockId },
        type: 'GET',
        timeout: 10000,
        success: function (data) {
            const finalData = parseStockData(data);
            updatePriceDisplay(finalData);
        },
        error: function (xhr, status, error) {
            console.error('請求失敗:', error);
        }
    });
}

function updateMiniKLineChart(priceData) {
    const now = new Date();
    const timeLabel = now.getHours().toString().padStart(2, '0') + ':' +
        now.getMinutes().toString().padStart(2, '0') + ':' +
        now.getSeconds().toString().padStart(2, '0');

    const openPrice = parseFloat(priceData.openPrice || priceData.currentPrice);
    const highPrice = parseFloat(priceData.highPrice || priceData.currentPrice);
    const lowPrice = parseFloat(priceData.lowPrice || priceData.currentPrice);
    const closePrice = parseFloat(priceData.currentPrice || 0);

    if (closePrice <= 0) return;

    miniKlineData.push([timeLabel, openPrice, closePrice, lowPrice, highPrice]);

    if (miniKlineData.length > 30) {
        miniKlineData.shift();
    }

    miniChart.setOption({
        xAxis: { data: miniKlineData.map(item => item[0]) },
        series: [{ data: miniKlineData.map(item => [item[1], item[2], item[3], item[4]]) }]
    });
}

function updatePriceDisplay(priceData) {
    // 使用共用格式化
    $('#realtimePrice').text(`$${StockFormatter.formatPrice(priceData.currentPrice)}`);

    // 使用共用漲跌計算（含顏色和百分比）
    const changeInfo = StockFormatter.calculateChange(priceData.currentPrice, priceData.yesterdayPrice);
    $('#realtimeChange').html(`<span class="${changeInfo.className}">${changeInfo.text}</span>`);

    $('#updateTime').text(priceData.updateTime);

    updateMiniKLineChart(priceData);

}

function initMiniKLineChart() {
    var chartDom = document.getElementById('miniKLineChart');
    miniChart = echarts.init(chartDom);

    var option = {
        grid: { left: '5%', right: '5%', top: '5%', bottom: '10%' },
        xAxis: { type: 'category', data: [], axisLabel: { fontSize: 10 } },
        yAxis: {
            type: 'value',
            scale: true,
            splitLine: { show: true },
            axisLabel: { fontSize: 10, formatter: function (value) { return value.toFixed(1); } }
        },
        series: [{
            type: 'candlestick',
            data: [],
            itemStyle: {
                color: '#ef4444',
                color0: '#10b981',
                borderColor: '#ef4444',
                borderColor0: '#10b981'
            }
        }]
    };

    miniChart.setOption(option);
}

let realtimeInterval;

function startRealtimeUpdates() {
    if (!currentStockId) {
        console.error('股票代號未設置');
        return;
    }

    getRealtimePriceAjax(currentStockId);
    realtimeInterval = setInterval(function () {
        getRealtimePriceAjax(currentStockId);
    }, 5000);
}

$(document).ready(function () {
    currentStockId = $('#realtimeDot').data('stock-id');
    initMiniKLineChart();
    startRealtimeUpdates();
});
