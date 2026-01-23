<template>
    <nav class="navbar">
        <div class="container">
            <RouterLink to="/" class="brand">
                <span class="brand-icon">📈</span>
                <span class="brand-text">MetaStock</span>
            </RouterLink>
            <div class="nav-links">
                <RouterLink v-for="link in links" :key="link.to" :to="link.to">
                    {{ link.label }}
                </RouterLink>
            </div>
        </div>
    </nav>
</template>

<script setup>
defineProps({
    links: {
        type: Array,
        default: () => [
            { to: '/', label: '首頁' },
            { to: '/stock/2330', label: '飆股雷達分析' },
            { to: '/stock/2317', label: '題材型短線' },
            { to: '/stock/2454', label: '聯發科' }
        ]
    }
})
</script>

<style scoped>
.navbar {
    background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
    padding: 1rem 0;
    position: sticky;
    top: 0;
    z-index: 100;
    box-shadow: 0 2px 20px rgba(0, 0, 0, 0.3);
}

.navbar .container {
    display: flex;
    justify-content: space-between;
    align-items: center;
    max-width: 1400px;
    margin: 0 auto;
    padding: 0 2rem;
}

.brand {
    display: flex;
    align-items: center;
    gap: 0.5rem;
    text-decoration: none;
    font-size: 1.5rem;
    font-weight: 700;
}

.brand-icon {
    font-size: 1.8rem;
}

.brand-text {
    background: linear-gradient(135deg, #00d4ff, #00ff88);
    -webkit-background-clip: text;
    -webkit-text-fill-color: transparent;
    background-clip: text;
}

.nav-links {
    display: flex;
    gap: 1.5rem;
}

.nav-links a {
    color: #a0a0a0;
    text-decoration: none;
    font-weight: 500;
    transition: color 0.3s ease;
    padding: 0.5rem 1rem;
    border-radius: 8px;
}

.nav-links a:hover,
.nav-links a.router-link-active {
    color: #00d4ff;
    background: rgba(0, 212, 255, 0.1);
}

.main-content {
    min-height: calc(100vh - 160px);
}

.footer {
    background: #1a1a2e;
    padding: 1.5rem 0;
    text-align: center;
    color: #666;
    font-size: 0.9rem;
}
</style>
