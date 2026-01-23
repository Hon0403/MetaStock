/* wwwroot/js/kline-Chart.js */
var currentStockId;
var klineChart;
var currentTimeframe = '1M';
var klineDataCache = {};
var isLoading = false;

// 初始化股票代號
function initializeStockId() {
    var container = document.getElementById('klineChart');
    currentStockId = container.dataset.stockId;  // 從 data-stock-id 讀取
}

// 頁面初始化

$(document).ready(function () {
    initializeStockId();     //  取得股票代號
    initKLineChart();        //  初始化K線圖
});

// 初始化K線圖
function initKLineChart() {
    var chartDom = document.getElementById('klineChart');
    var stockId = chartDom.dataset.stockId;

    klineChart = echarts.init(chartDom);

    // 監聽視窗大小變化，讓圖表自適應
    window.addEventListener('resize', function () {
        klineChart.resize();
    });

    loadKLineData('1M', stockId);
}

// 載入K線資料
function loadKLineData(timeframe) {
    isLoading = true;
    $.ajax({
        url: '/api/KLineChart',
        data: { stockId: currentStockId, timeframe: timeframe },
        type: 'GET',
        timeout: 30000,
        success: function (data) {
            console.log('✅ AJAX成功收到資料:', data);
            isLoading = false;
            hideKLineLoading();
            if (data.error) {
                showKLineError(`載入失敗: ${data.error}`);
                return;
            }

            klineDataCache[timeframe] = data;
            processKLineData(data, timeframe);
        },
        error: function (xhr, status, error) {
            isLoading = false;
            hideKLineLoading();
            showKLineError(`載入失敗: ${error}`);
        }
    });
}

function processKLineData(data, timeframe) {
    if (!data || !data.dates || data.dates.length === 0) {
        showKLineError('無資料可顯示');
        return;
    }
    updateKLineChart(data);
    updateDataStatistics(data, timeframe);
}

// 更新K線圖
function updateKLineChart(data) {
    if (!data || !data.dates || data.dates.length === 0) {
        console.log('K線資料為空');
        return;
    }

    var option = {
        title: { text: '' },
        tooltip: {
            trigger: 'axis',
            axisPointer: { type: 'cross' },
            formatter: function (params) {
                var result = '';

                // 顯示日期
                result += params[0].axisValue + '<br/>';

                // 處理 K線資料
                var klineData = params.find(item => item.seriesType === 'candlestick');
                if (klineData && klineData.data) {
                    var data = klineData.data;
                    result += klineData.marker + ' ' + klineData.seriesName + '<br/>';
                    result += '開盤價：<span style="font-weight:bold">' + parseFloat(data[1]).toFixed(2) + '</span><br/>';
                    result += '最高價：<span style="font-weight:bold">' + parseFloat(data[4]).toFixed(2) + '</span><br/>';
                    result += '最低價：<span style="font-weight:bold">' + parseFloat(data[3]).toFixed(2) + '</span><br/>';
                    result += '收盤價：<span style="font-weight:bold">' + parseFloat(data[2]).toFixed(2) + '</span><br/>';
                }

                // 處理成交量資料
                var volumeData = params.find(item => item.seriesType === 'bar');
                if (volumeData) {
                    result += volumeData.marker + ' 成交量：<span style="font-weight:bold">' +
                        (volumeData.data || 0).toLocaleString() + '</span><br/>';
                }

                return result;
            }
        },
        legend: { data: ['K線', '成交量'] },
        dataZoom: [
            {
                type: 'inside',           // 內建模式，不顯示滑塊
                xAxisIndex: [0, 1],       // 控制兩個 x 軸（K線圖和成交量）
                start: 70,                // 初始顯示範圍開始位置 (70%)
                end: 100,                 // 初始顯示範圍結束位置 (100%)
            }
        ],
        grid: [
            { left: '8%', right: '8%', height: '50%' },
            { left: '8%', right: '8%', top: '70%', height: '25%' }
        ],
        xAxis: [
            { type: 'category', data: data.dates, boundaryGap: false },
            { type: 'category', gridIndex: 1, data: data.dates }
        ],
        yAxis: [
            { scale: true, splitArea: { show: true } },
            { scale: true, gridIndex: 1, splitNumber: 2 }
        ],
        series: [
            {
                name: 'K線',
                type: 'candlestick',
                data: data.klineData,
                barMaxWidth: 12,
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
    // 設定圖表選項
    klineChart.setOption(option);
}

// 切換時間框架
function changeTimeframe(timeframe) {
    if (isLoading) return;

    currentTimeframe = timeframe;

    // 更新按鈕狀態
    $('.btn-toolbar .btn').removeClass('active');
    $(`[onclick="changeTimeframe('${timeframe}')"]`).addClass('active');

    // 顯示載入狀態
    showKLineLoading(timeframe);

    // 檢查快取
    if (klineDataCache[timeframe]) {
        processKLineData(klineDataCache[timeframe], timeframe);
        hideKLineLoading();
        return;
    }

    // 載入新資料
    loadKLineData(timeframe);
}

// 載入狀態
function showKLineLoading(timeframe) {
    var descriptions = {
        '7D': '近7個交易日', '10D': '近10個交易日', '15D': '近15個交易日',
        '1M': '近1個月', '3M': '近3個月', '6M': '近半年', '1Y': '近1年'
    };
    $('#loadingTimeframe').text(descriptions[timeframe]);
    $('#klineLoadingIndicator').removeClass('d-none');
    $('#klineChart').css('opacity', '0.5');
}

function hideKLineLoading() {
    $('#klineLoadingIndicator').addClass('d-none');
    $('#klineChart').css('opacity', '1');
}

// 更新統計
function updateDataStatistics(data, timeframe) {
    if (!data || !data.dates || data.dates.length === 0) {
        return;
    }
    console.log('統一格式');

    // 使用統一後端格式
    $('#dataDateRange').text(`${data.dates[0]} ~ ${data.dates[data.dates.length - 1]}`);
    $('#tradingDays').text(`${data.dates.length}日`);
    $('#lastUpdateTime').text(new Date().toLocaleTimeString());
}

// 重新載入
function refreshKLineData() {
    delete klineDataCache[currentTimeframe];
    changeTimeframe(currentTimeframe);
}

// 錯誤處理
function showKLineError(message) {
    $('#klineChart').html(`
        <div class="text-center p-5">
            <div class="text-danger mb-3">
                <i class="fas fa-exclamation-triangle fa-3x"></i>
            </div>
            <h6 class="text-danger">${message}</h6>
            <button class="btn btn-sm btn-outline-primary mt-2" onclick="refreshKLineData()">
                重新載入
            </button>
        </div>
    `);
}


