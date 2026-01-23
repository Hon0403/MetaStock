import axios from 'axios'

const api = axios.create({
    baseURL: '/api',
    timeout: 10000,
    headers: {
        'Content-Type': 'application/json'
    }
})

export default {
    // K 線圖資料
    async getKLineData(stockId, timeframe = '3M') {
        const response = await api.get('/KLineChart', {
            params: { stockId, timeframe }
        })
        return response.data
    },

    // 即時股價
    async getRealtimePrice(stockId) {
        const response = await api.get('/RealtimePrice', {
            params: { stockId }
        })
        return response.data
    },

    // 股票基本資訊
    async getStockBasicInfo(stockId) {
        const response = await api.get('/StockBasicInfo', {
            params: { stockId }
        })
        return response.data
    },

    // 股票資料 (TWSE)
    async getStockData(stockId) {
        const response = await api.get('/Stock/data', {
            params: { stockId }
        })
        return response.data
    },

    // 歷史資料
    async getHistoricalData(stockId) {
        const response = await api.get('/Stock/historical', {
            params: { stockId }
        })
        return response.data
    }
}
