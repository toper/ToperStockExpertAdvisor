<script setup lang="ts">
import { ChartBarIcon, Cog6ToothIcon } from '@heroicons/vue/24/outline'
</script>

<template>
  <header class="bg-white border-b border-gray-200 shadow-sm">
    <div class="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
      <div class="flex justify-between items-center h-16">
        <div class="flex items-center gap-3">
          <ChartBarIcon class="h-8 w-8 text-primary-600" />
          <div>
            <h1 class="text-xl font-bold text-gray-900">Toper Stock Expert</h1>
            <p class="text-xs text-gray-500">PUT Options Advisor</p>
          </div>
        </div>
        <nav class="flex items-center gap-4">
          <button class="p-2 text-gray-500 hover:text-gray-700 hover:bg-gray-100 rounded-lg transition-colors">
            <Cog6ToothIcon class="h-5 w-5" />
          </button>
        </nav>
      </div>
    </div>
  </header>
</template>
