<script setup>
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import api from '../services/api'

const router = useRouter()
const searchQuery = ref('')
const popularStocks = ref([
  { id: '2330', name: '台積電', price: '--', change: '--', changeClass: 'price-neutral' },
  { id: '2317', name: '鴻海', price: '--', change: '--', changeClass: 'price-neutral' },
  { id: '2454', name: '聯發科', price: '--', change: '--', changeClass: 'price-neutral' },
  { id: '2882', name: '國泰金', price: '--', change: '--', changeClass: 'price-neutral' },
  { id: '2884', name: '玉山金', price: '--', change: '--', changeClass: 'price-neutral' },
  { id: '2412', name: '中華電', price: '--', change: '--', changeClass: 'price-neutral' }
])
const loading = ref(true)

const searchStock = () => {
  if (searchQuery.value.trim()) {
    router.push(`/stock/${searchQuery.value.trim()}`)
  }
}

const loadStockPrices = async () => {
  try {
    for (const stock of popularStocks.value) {
      try {
        const data = await api.getRealtimePrice(stock.id)
        if (data.msgArray && data.msgArray.length > 0) {
          const info = data.msgArray[0]
          stock.price = info.z || info.y || '--'
          const change = parseFloat(info.z) - parseFloat(info.y)
          if (!isNaN(change)) {
            const changePercent = ((change / parseFloat(info.y)) * 100).toFixed(2)
            stock.change = `${change >= 0 ? '+' : ''}${change.toFixed(2)} (${change >= 0 ? '+' : ''}${changePercent}%)`
            stock.changeClass = change > 0 ? 'price-up' : change < 0 ? 'price-down' : 'price-neutral'
          }
        }
      } catch (err) {
        console.log(`Failed to load ${stock.id}`)
      }
    }
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  loadStockPrices()
})
</script>

<template>
  <div class="home">
    <!-- Hero Section -->
    <section class="hero">
      <div class="container">
        <h1 class="hero-title">
          <span class="gradient-text">MetaStock</span>
          <span class="subtitle">台股即時看盤系統</span>
        </h1>
        <p class="hero-description">
          即時股價追蹤、K線分析、技術指標，一站式股票分析平台
        </p>
        
        <!-- Search Box -->
        <div class="search-container">
          <input 
            v-model="searchQuery"
            type="text" 
            placeholder="輸入股票代號 (例如: 2330)" 
            class="search-input"
            @keyup.enter="searchStock"
          >
          <button @click="searchStock" class="btn btn-primary search-btn">
            🔍 查詢
          </button>
        </div>
      </div>
    </section>

    <!-- Popular Stocks -->
    <section class="stocks-section">
      <div class="container">
        <h2 class="section-title">熱門股票</h2>
        
        <div v-if="loading" class="loading">
          <div class="spinner"></div>
        </div>
        
        <div v-else class="stocks-grid">
          <RouterLink 
            v-for="stock in popularStocks" 
            :key="stock.id"
            :to="`/stock/${stock.id}`"
            class="stock-card card"
          >
            <div class="stock-header">
              <span class="stock-id">{{ stock.id }}</span>
              <span class="stock-name">{{ stock.name }}</span>
            </div>
            <div class="stock-price" :class="stock.changeClass">
              {{ stock.price }}
            </div>
            <div class="stock-change" :class="stock.changeClass">
              {{ stock.change }}
            </div>
          </RouterLink>
        </div>
      </div>
    </section>

    <!-- Features Section -->
    <section class="features-section">
      <div class="container">
        <h2 class="section-title">功能特色</h2>
        <div class="features-grid">
          <div class="feature-card card">
            <div class="feature-icon">📊</div>
            <h3>K線圖分析</h3>
            <p>支援多種時間區間，從 5 日到 5 年完整歷史資料</p>
          </div>
          <div class="feature-card card">
            <div class="feature-icon">⚡</div>
            <h3>即時報價</h3>
            <p>盤中即時更新股價，不錯過任何交易機會</p>
          </div>
          <div class="feature-card card">
            <div class="feature-icon">📈</div>
            <h3>技術指標</h3>
            <p>MA均線、成交量分析，輔助投資決策</p>
          </div>
        </div>
      </div>
    </section>
  </div>
</template>

<style scoped>
.hero {
  background: linear-gradient(135deg, #1a1a2e 0%, #16213e 50%, #0f3460 100%);
  padding: 6rem 0 4rem;
  text-align: center;
}

.hero-title {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  margin-bottom: 1.5rem;
}

.gradient-text {
  font-size: 4rem;
  font-weight: 800;
  background: linear-gradient(135deg, #00d4ff, #00ff88, #00d4ff);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
  animation: gradient 3s ease infinite;
  background-size: 200% 200%;
}

@keyframes gradient {
  0%, 100% { background-position: 0% 50%; }
  50% { background-position: 100% 50%; }
}

.subtitle {
  font-size: 1.5rem;
  color: var(--text-secondary);
  font-weight: 400;
}

.hero-description {
  color: var(--text-muted);
  font-size: 1.1rem;
  margin-bottom: 2.5rem;
}

.search-container {
  display: flex;
  gap: 1rem;
  max-width: 500px;
  margin: 0 auto;
}

.search-input {
  flex: 1;
  padding: 1rem 1.5rem;
  border: 2px solid rgba(255, 255, 255, 0.1);
  border-radius: 12px;
  background: rgba(255, 255, 255, 0.05);
  color: var(--text-primary);
  font-size: 1rem;
  transition: all 0.3s ease;
}

.search-input:focus {
  outline: none;
  border-color: var(--primary);
  background: rgba(0, 212, 255, 0.05);
}

.search-btn {
  padding: 1rem 2rem;
}

.stocks-section,
.features-section {
  padding: 4rem 0;
}

.section-title {
  font-size: 1.8rem;
  margin-bottom: 2rem;
  text-align: center;
}

.stocks-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
  gap: 1.5rem;
}

.stock-card {
  text-decoration: none;
  text-align: center;
  cursor: pointer;
}

.stock-header {
  display: flex;
  justify-content: space-between;
  margin-bottom: 1rem;
}

.stock-id {
  color: var(--primary);
  font-weight: 600;
}

.stock-name {
  color: var(--text-secondary);
}

.stock-price {
  font-size: 1.8rem;
  font-weight: 700;
  margin-bottom: 0.5rem;
}

.stock-change {
  font-size: 0.9rem;
  font-weight: 500;
}

.features-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
  gap: 2rem;
}

.feature-card {
  text-align: center;
  padding: 2rem;
}

.feature-icon {
  font-size: 3rem;
  margin-bottom: 1rem;
}

.feature-card h3 {
  margin-bottom: 0.5rem;
  color: var(--primary);
}

.feature-card p {
  color: var(--text-secondary);
}

@media (max-width: 768px) {
  .gradient-text {
    font-size: 2.5rem;
  }
  
  .search-container {
    flex-direction: column;
  }
}
</style>
