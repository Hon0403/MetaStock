/* wwwroot/js/stock-detail.js */
var currentStockId;
var klineChart;

// 初始化股票代號（從HTML中的隱藏欄位取得）
function initializeStockId() {

    var urlParams = new URLSearchParams(window.location.search);
    currentStockId = urlParams.get('id') || currentStockId;
}

// 初始化K線圖
function initKLineChart() {
    var chartDom = document.getElementById('klineChart');
    klineChart = echarts.init(chartDom);
    loadKLineData('D');
}

// 載入K線資料
function loadKLineData(timeframe) {
    $.ajax({
        url: '/Stock/GetKLineData',
        data: { stockId: currentStockId, timeframe: timeframe },
        type: 'GET',
        success: function (data) {
            updateKLineChart(data);
        },
        error: function () {
            console.log('無法載入K線資料');
        }
    });
}

// 更新K線圖
function updateKLineChart(data) {
    var option = {
        title: { text: '' },
        tooltip: {
            trigger: 'axis',
            axisPointer: { type: 'cross' }
        },
        legend: { data: ['K線', '成交量'] },
        grid: [
            { left: '10%', right: '8%', height: '65%' },
            { left: '10%', right: '8%', top: '75%', height: '16%' }
        ],
        xAxis: [
            {
                type: 'category',
                data: data.dates,
                boundaryGap: false,
                axisLine: { onZero: false },
                splitLine: { show: false },
                min: 'dataMin',
                max: 'dataMax'
            },
            {
                type: 'category',
                gridIndex: 1,
                data: data.dates,
                boundaryGap: false,
                axisLine: { onZero: false },
                axisTick: { show: false },
                splitLine: { show: false },
                axisLabel: { show: false },
                min: 'dataMin',
                max: 'dataMax'
            }
        ],
        yAxis: [
            { scale: true, splitArea: { show: true } },
            {
                scale: true,
                gridIndex: 1,
                splitNumber: 2,
                axisLabel: { show: false },
                axisLine: { show: false },
                axisTick: { show: false },
                splitLine: { show: false }
            }
        ],
        series: [
            {
                name: 'K線',
                type: 'candlestick',
                data: data.klineData,
                itemStyle: {
                    color: '#ef4444',
                    color0: '#10b981',
                    borderColor: '#ef4444',
                    borderColor0: '#10b981'
                }
            },
            {
                name: '成交量',
                type: 'bar',
                xAxisIndex: 1,
                yAxisIndex: 1,
                data: data.volumes
            }
        ]
    };
    klineChart.setOption(option);
}

// 切換時間框架
function changeTimeframe(timeframe) {
    $('.btn-group .btn').removeClass('active');
    $(event.target).addClass('active');
    loadKLineData(timeframe);
}

// 載入股票資料
function loadStockData() {
    $.ajax({
        url: '/Stock/GetStockData',
        data: { stockId: currentStockId },
        type: 'GET',
        success: function (data) {
            updateStockInfo(data);
        },
        error: function () {
            console.log('無法載入股票資料');
        }
    });
}

// 更新股票資訊
function updateStockInfo(data) {
    $('#stockTitle').text(data.stockId + ' ' + data.stockName);
    $('#stockId').text(data.stockId);
    $('#stockName').text(data.stockName);
    $('#currentPrice').text(data.currentPrice).removeClass('price-up price-down').addClass(data.priceChangeClass);
    $('#priceChange').text(data.priceChange).removeClass('price-up price-down').addClass(data.priceChangeClass);
    $('#volume').text(data.volume + ' 張');
    $('#pe').text(data.pe || 'N/A');
    $('#openPrice').text(data.openPrice);
    $('#highPrice').text(data.highPrice);
    $('#lowPrice').text(data.lowPrice);
    $('#prevClose').text(data.prevClose);
}

// 載入歷史資料
function loadHistoricalData() {
    $.ajax({
        url: '/Stock/GetHistoricalData',
        data: { stockId: currentStockId },
        type: 'GET',
        success: function (data) {
            updateHistoricalTable(data);
        },
        error: function () {
            console.log('無法載入歷史資料');
        }
    });
}

// 更新歷史資料表格
function updateHistoricalTable(data) {
    var tbody = $('#historyTable tbody');
    tbody.empty();

    $.each(data, function (index, item) {
        var changeClass = item.change >= 0 ? 'price-up' : 'price-down';
        var row = '<tr>' +
            '<td>' + item.date + '</td>' +
            '<td>' + item.open + '</td>' +
            '<td class="price-up">' + item.high + '</td>' +
            '<td class="price-down">' + item.low + '</td>' +
            '<td class="' + changeClass + '"><strong>' + item.close + '</strong></td>' +
            '<td>' + item.volume + '</td>' +
            '<td class="' + changeClass + '">' + item.changePercent + '</td>' +
            '</tr>';
        tbody.append(row);
    });
}

// 重新整理資料
function refreshData() {
    var button = $(event.target);
    var originalText = button.html();
    button.html('<span class="loading"></span> 更新中...').prop('disabled', true);

    setTimeout(function () {
        loadStockData();
        loadHistoricalData();
        button.html(originalText).prop('disabled', false);
    }, 2000);
}

// 頁面初始化
$(document).ready(function () {
    initializeStockId();  // 初始化股票代號
    initKLineChart();
    loadStockData();
    loadHistoricalData();
});