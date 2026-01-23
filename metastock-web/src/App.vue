<script setup>
import { RouterView } from 'vue-router'
import NavBar from './components/NavBar.vue'
</script>

<template>
  <div id="app">
    <NavBar />

    <main class="main-content">
      <RouterView />
    </main>

    <footer class="footer">
      <!-- ... -->
    </footer>
  </div>
</template>
