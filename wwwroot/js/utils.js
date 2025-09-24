// ===== 股票資料格式化相關 =====
const StockFormatter = {
    // 格式化價格（保留2位小數）
    formatPrice: function (priceString) {
        if (!priceString || priceString === '-') return '--';
        return parseFloat(priceString).toFixed(2);
    },

    // 格式化成交量
    formatVolume: function (volume) {
        if (!volume || volume === '-') return '--';
        const num = parseInt(volume);
        if (num >= 1000000) return (num / 1000000).toFixed(1) + 'M';
        if (num >= 1000) return (num / 1000).toFixed(0) + 'K';
        return num.toLocaleString();
    },

    // 計算漲跌
    calculateChange: function (current, previous) {
        const change = parseFloat(current || 0) - parseFloat(previous || 0);
        const percent = (change / parseFloat(previous || 1) * 100).toFixed(2);
        return {
            text: `${change >= 0 ? '+' : ''}${change.toFixed(2)} (${percent}%)`,
            className: change >= 0 ? 'text-success' : 'text-danger'
        };
    }
};

// 全域可用
window.StockFormatter = StockFormatter;
