import { createRouter, createWebHistory } from 'vue-router'
import HomeView from '../views/HomeView.vue'
import StockDetailView from '../views/StockDetailView.vue'

const routes = [
    {
        path: '/',
        name: 'home',
        component: HomeView
    },
    {
        path: '/stock/:id',
        name: 'stock-detail',
        component: StockDetailView
    }
]

const router = createRouter({
    history: createWebHistory(),
    routes
})

export default router
