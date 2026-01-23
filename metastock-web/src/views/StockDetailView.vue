<script setup>
import { ref, onMounted, onUnmounted, watch } from 'vue'
import { useRoute } from 'vue-router'
import * as echarts from 'echarts'
import api from '../services/api'

const route = useRoute()
const stockId = ref(route.params.id)

// Stock info
const stockInfo = ref({
  stockId: '--',
  stockName: '--',
  currentPrice: '--',
  priceChange: '--',
  priceChangeClass: 'price-neutral',
  openPrice: '--',
  highPrice: '--',
  lowPrice: '--',
  prevClose: '--',
  volume: '--'
})

// Real-time data
const realtimePrice = ref('--')
const realtimeChange = ref('--')
const realtimeClass = ref('price-neutral')
const updateTime = ref('--')

// Chart
const chartContainer = ref(null)
let chart = null
const currentTimeframe = ref('3M')
const loading = ref(true)

// Timeframe options
const timeframes = [
  { value: '7D', label: '近5日', group: 'short' },
  { value: '1M', label: '近1月', group: 'short' },
  { value: '3M', label: '近3月', group: 'medium' },
  { value: '6M', label: '近半年', group: 'medium' },
  { value: '1Y', label: '近1年', group: 'medium' },
  { value: '2Y', label: '近2年', group: 'long' },
  { value: '5Y', label: '近5年', group: 'long' },
  { value: 'YTD', label: '年初至今', group: 'special' },
  { value: 'MAX', label: '全部', group: 'special' }
]

// Load K-Line data
const loadKLineData = async (timeframe) => {
  loading.value = true
  currentTimeframe.value = timeframe
  
  try {
    const data = await api.getKLineData(stockId.value, timeframe)
    
    if (data.error) {
      console.error(data.error)
      return
    }
    
    renderChart(data)
  } catch (err) {
    console.error('Failed to load K-Line data:', err)
  } finally {
    loading.value = false
  }
}

// Render ECharts
const renderChart = (data) => {
  if (!chartContainer.value) return
  
  if (!chart) {
    chart = echarts.init(chartContainer.value)
  }
  
  const upColor = '#00ff88'
  const downColor = '#ff4757'
  
  const option = {
    backgroundColor: '#1a1a2e',
    animation: false,
    tooltip: {
      trigger: 'axis',
      axisPointer: {
        type: 'cross'
      },
      backgroundColor: 'rgba(26, 26, 46, 0.9)',
      borderColor: '#00d4ff',
      textStyle: { color: '#fff' }
    },
    legend: {
      data: ['K線', 'MA5', 'MA20'],
      textStyle: { color: '#a0a0a0' },
      top: 10
    },
    grid: [
      { left: '10%', right: '8%', height: '55%' },
      { left: '10%', right: '8%', top: '75%', height: '15%' }
    ],
    xAxis: [
      {
        type: 'category',
        data: data.dates,
        axisLine: { lineStyle: { color: '#333' } },
        axisLabel: { color: '#a0a0a0' }
      },
      {
        type: 'category',
        gridIndex: 1,
        data: data.dates,
        axisLabel: { show: false }
      }
    ],
    yAxis: [
      {
        scale: true,
        axisLine: { lineStyle: { color: '#333' } },
        axisLabel: { color: '#a0a0a0' },
        splitLine: { lineStyle: { color: '#252540' } }
      },
      {
        scale: true,
        gridIndex: 1,
        splitNumber: 2,
        axisLabel: { show: false },
        splitLine: { show: false }
      }
    ],
    dataZoom: [
      {
        type: 'inside',
        xAxisIndex: [0, 1],
        start: 0,
        end: 100
      },
      {
        show: true,
        xAxisIndex: [0, 1],
        type: 'slider',
        top: '93%',
        start: 0,
        end: 100
      }
    ],
    series: [
      {
        name: 'K線',
        type: 'candlestick',
        data: data.klineData,
        itemStyle: {
          color: upColor,
          color0: downColor,
          borderColor: upColor,
          borderColor0: downColor
        }
      },
      {
        name: '成交量',
        type: 'bar',
        xAxisIndex: 1,
        yAxisIndex: 1,
        data: data.volumes,
        itemStyle: {
          color: (params) => {
            const kData = data.klineData[params.dataIndex]
            return kData[1] >= kData[0] ? upColor : downColor
          }
        }
      }
    ]
  }
  
  // Calculate MA
  if (data.klineData && data.klineData.length > 0) {
    const closes = data.klineData.map(d => d[1])
    const ma5 = calculateMA(closes, 5)
    const ma20 = calculateMA(closes, 20)
    
    option.series.push({
      name: 'MA5',
      type: 'line',
      data: ma5,
      smooth: true,
      lineStyle: { width: 1, color: '#ffa502' },
      showSymbol: false
    })
    
    option.series.push({
      name: 'MA20',
      type: 'line',
      data: ma20,
      smooth: true,
      lineStyle: { width: 1, color: '#ff6b81' },
      showSymbol: false
    })
  }
  
  chart.setOption(option)
}

// Calculate Moving Average
const calculateMA = (data, period) => {
  const result = []
  for (let i = 0; i < data.length; i++) {
    if (i < period - 1) {
      result.push('-')
    } else {
      let sum = 0
      for (let j = 0; j < period; j++) {
        sum += data[i - j]
      }
      result.push((sum / period).toFixed(2))
    }
  }
  return result
}

// Load stock basic info
const loadStockInfo = async () => {
  try {
    const data = await api.getStockData(stockId.value)
    if (!data.error) {
      stockInfo.value = data
    }
  } catch (err) {
    console.error('Failed to load stock info:', err)
  }
}

// Load realtime price
let realtimeInterval = null
const loadRealtimePrice = async () => {
  try {
    const data = await api.getRealtimePrice(stockId.value)
    if (data.msgArray && data.msgArray.length > 0) {
      const info = data.msgArray[0]
      realtimePrice.value = info.z || info.y || '--'
      
      const change = parseFloat(info.z) - parseFloat(info.y)
      if (!isNaN(change)) {
        const changePercent = ((change / parseFloat(info.y)) * 100).toFixed(2)
        realtimeChange.value = `${change >= 0 ? '+' : ''}${change.toFixed(2)} (${change >= 0 ? '+' : ''}${changePercent}%)`
        realtimeClass.value = change > 0 ? 'price-up' : change < 0 ? 'price-down' : 'price-neutral'
      }
      
      updateTime.value = info.t || new Date().toLocaleTimeString()
    }
  } catch (err) {
    console.error('Failed to load realtime price:', err)
  }
}

// Watch route changes
watch(() => route.params.id, (newId) => {
  stockId.value = newId
  loadStockInfo()
  loadKLineData(currentTimeframe.value)
  loadRealtimePrice()
})

onMounted(() => {
  loadStockInfo()
  loadKLineData(currentTimeframe.value)
  loadRealtimePrice()
  
  // Start realtime updates
  realtimeInterval = setInterval(loadRealtimePrice, 5000)
  
  // Handle resize
  window.addEventListener('resize', () => {
    chart?.resize()
  })
})

onUnmounted(() => {
  if (realtimeInterval) {
    clearInterval(realtimeInterval)
  }
  chart?.dispose()
})
</script>

<template>
  <div class="stock-detail">
    <div class="container">
      <!-- Header -->
      <div class="stock-header">
        <div class="stock-title">
          <h1>
            <span class="stock-id-badge">{{ stockId }}</span>
            <span class="stock-name">{{ stockInfo.stockName }}</span>
          </h1>
        </div>
        <div class="stock-price-display" :class="stockInfo.priceChangeClass">
          <span class="current-price">{{ stockInfo.currentPrice }}</span>
          <span class="price-change">{{ stockInfo.priceChange }}</span>
        </div>
      </div>

      <div class="content-grid">
        <!-- Chart Section -->
        <div class="chart-section card">
          <div class="chart-header">
            <h2>📊 K線圖</h2>
            <div class="timeframe-buttons">
              <button 
                v-for="tf in timeframes" 
                :key="tf.value"
                :class="['timeframe-btn', { active: currentTimeframe === tf.value }]"
                @click="loadKLineData(tf.value)"
              >
                {{ tf.label }}
              </button>
            </div>
          </div>
          
          <div v-if="loading" class="loading">
            <div class="spinner"></div>
          </div>
          
          <div ref="chartContainer" class="chart-container"></div>
        </div>

        <!-- Side Panel -->
        <div class="side-panel">
          <!-- Realtime Panel -->
          <div class="realtime-panel card">
            <div class="panel-header realtime-header">
              <h3>⚡ 即時盯盤</h3>
              <span class="live-dot"></span>
            </div>
            <div class="realtime-content">
              <div class="realtime-price" :class="realtimeClass">
                {{ realtimePrice }}
              </div>
              <div class="realtime-change" :class="realtimeClass">
                {{ realtimeChange }}
              </div>
              <div class="update-time">
                更新時間：{{ updateTime }}
              </div>
            </div>
          </div>

          <!-- Info Panel -->
          <div class="info-panel card">
            <div class="panel-header">
              <h3>📋 基本資訊</h3>
            </div>
            <div class="info-grid">
              <div class="info-item">
                <span class="label">開盤</span>
                <span class="value">{{ stockInfo.openPrice }}</span>
              </div>
              <div class="info-item">
                <span class="label">最高</span>
                <span class="value price-up">{{ stockInfo.highPrice }}</span>
              </div>
              <div class="info-item">
                <span class="label">昨收</span>
                <span class="value">{{ stockInfo.prevClose }}</span>
              </div>
              <div class="info-item">
                <span class="label">最低</span>
                <span class="value price-down">{{ stockInfo.lowPrice }}</span>
              </div>
              <div class="info-item full-width">
                <span class="label">成交量</span>
                <span class="value">{{ stockInfo.volume }} 千股</span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.stock-detail {
  padding: 2rem 0;
}

.stock-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 2rem;
  padding: 1.5rem 2rem;
  background: var(--bg-card);
  border-radius: 16px;
}

.stock-title h1 {
  display: flex;
  align-items: center;
  gap: 1rem;
}

.stock-id-badge {
  background: var(--primary);
  color: #000;
  padding: 0.25rem 0.75rem;
  border-radius: 8px;
  font-size: 1rem;
  font-weight: 600;
}

.stock-name {
  font-size: 1.8rem;
}

.stock-price-display {
  text-align: right;
}

.current-price {
  display: block;
  font-size: 2.5rem;
  font-weight: 700;
}

.price-change {
  font-size: 1.1rem;
}

.content-grid {
  display: grid;
  grid-template-columns: 1fr 350px;
  gap: 1.5rem;
}

.chart-section {
  padding: 1.5rem;
}

.chart-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
  flex-wrap: wrap;
  gap: 1rem;
}

.chart-header h2 {
  font-size: 1.2rem;
}

.chart-container {
  width: 100%;
  height: 500px;
}

.side-panel {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.panel-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
  padding-bottom: 0.75rem;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
}

.panel-header h3 {
  font-size: 1rem;
}

.realtime-header {
  background: linear-gradient(135deg, #1a472a, #0a2f1a);
  margin: -1.5rem -1.5rem 1rem;
  padding: 1rem 1.5rem;
  border-radius: 16px 16px 0 0;
}

.live-dot {
  width: 10px;
  height: 10px;
  background: var(--success);
  border-radius: 50%;
  animation: pulse 2s infinite;
}

@keyframes pulse {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.5; }
}

.realtime-content {
  text-align: center;
}

.realtime-price {
  font-size: 2.5rem;
  font-weight: 700;
  margin-bottom: 0.5rem;
}

.realtime-change {
  font-size: 1.1rem;
  margin-bottom: 1rem;
}

.update-time {
  font-size: 0.85rem;
  color: var(--text-muted);
}

.info-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1rem;
}

.info-item {
  display: flex;
  justify-content: space-between;
}

.info-item.full-width {
  grid-column: 1 / -1;
}

.info-item .label {
  color: var(--text-muted);
}

.info-item .value {
  font-weight: 600;
}

@media (max-width: 1024px) {
  .content-grid {
    grid-template-columns: 1fr;
  }
  
  .chart-container {
    height: 400px;
  }
}

@media (max-width: 768px) {
  .stock-header {
    flex-direction: column;
    text-align: center;
    gap: 1rem;
  }
  
  .stock-price-display {
    text-align: center;
  }
  
  .chart-header {
    flex-direction: column;
  }
}
</style>
