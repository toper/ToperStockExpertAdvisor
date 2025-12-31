<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useRecommendationsStore } from '@/stores/recommendations'
import { format } from 'date-fns'
import {
  CheckCircleIcon,
  XCircleIcon,
  ArrowPathIcon,
  ClockIcon,
  ChartBarIcon
} from '@heroicons/vue/24/outline'

const store = useRecommendationsStore()

// Fetch stats on component mount
onMounted(() => {
  store.fetchStats()
})

const scanStatusText = computed(() => {
  if (!store.scanInProgress && !store.scanCompletedAt) {
    return 'No scan in progress'
  }

  if (store.scanInProgress) {
    return `Scanning ${store.currentScanSymbol}...`
  }

  if (store.scanErrorMessage) {
    return `Scan failed: ${store.scanErrorMessage}`
  }

  return 'Scan completed'
})

const scanStatusClass = computed(() => {
  if (store.scanInProgress) {
    return 'bg-blue-50 border-blue-200'
  }

  if (store.scanErrorMessage) {
    return 'bg-red-50 border-red-200'
  }

  if (store.scanCompletedAt) {
    return 'bg-green-50 border-green-200'
  }

  return 'bg-gray-50 border-gray-200'
})

const formattedDuration = computed(() => {
  if (store.scanDuration > 0) {
    const minutes = Math.floor(store.scanDuration / 60)
    const seconds = Math.floor(store.scanDuration % 60)
    return minutes > 0 ? `${minutes}m ${seconds}s` : `${seconds}s`
  }
  return '-'
})

const formattedStartTime = computed(() => {
  return store.scanStartedAt
    ? format(store.scanStartedAt, 'HH:mm:ss')
    : '-'
})

const formattedCompletedTime = computed(() => {
  return store.scanCompletedAt
    ? format(store.scanCompletedAt, 'HH:mm:ss')
    : '-'
})

const recentUpdates = computed(() => {
  // Show last 5 symbol updates
  return store.symbolUpdates.slice(-5).reverse()
})
</script>

<template>
  <div
    v-if="store.scanInProgress || store.scanCompletedAt"
    :class="['rounded-xl border-2 p-4 transition-all', scanStatusClass]"
  >
    <!-- Header -->
    <div class="flex items-center justify-between mb-4">
      <div class="flex items-center gap-3">
        <ArrowPathIcon
          v-if="store.scanInProgress"
          class="h-6 w-6 text-blue-600 animate-spin"
        />
        <CheckCircleIcon
          v-else-if="!store.scanErrorMessage && store.scanCompletedAt"
          class="h-6 w-6 text-green-600"
        />
        <XCircleIcon
          v-else-if="store.scanErrorMessage"
          class="h-6 w-6 text-red-600"
        />

        <div>
          <h3 class="text-lg font-semibold text-gray-900">
            Market Scan Status
          </h3>
          <p class="text-sm text-gray-600">{{ scanStatusText }}</p>
        </div>
      </div>

      <button
        v-if="!store.scanInProgress && store.scanCompletedAt"
        @click="store.clearScanProgress"
        class="text-sm text-gray-500 hover:text-gray-700 underline"
      >
        Clear
      </button>
    </div>

    <!-- Progress Bar -->
    <div v-if="store.totalSymbolsToScan > 0" class="mb-4">
      <div class="flex justify-between text-sm text-gray-600 mb-2">
        <span>Progress</span>
        <span class="font-medium">
          {{ store.scannedSymbolsCount }} / {{ store.totalSymbolsToScan }} symbols
          ({{ Math.round(store.scanProgressPercent) }}%)
        </span>
      </div>
      <div class="w-full bg-gray-200 rounded-full h-3 overflow-hidden">
        <div
          class="h-full bg-blue-600 transition-all duration-300 ease-out rounded-full"
          :style="{ width: `${store.scanProgressPercent}%` }"
        />
      </div>
    </div>

    <!-- Stats Grid -->
    <div class="grid grid-cols-2 md:grid-cols-4 gap-4 mb-4">
      <div class="bg-white rounded-lg p-3 border border-gray-200">
        <div class="flex items-center gap-2 text-gray-500 text-xs mb-1">
          <ClockIcon class="h-4 w-4" />
          Started
        </div>
        <div class="text-sm font-semibold text-gray-900">
          {{ formattedStartTime }}
        </div>
      </div>

      <div class="bg-white rounded-lg p-3 border border-gray-200">
        <div class="flex items-center gap-2 text-gray-500 text-xs mb-1">
          <ClockIcon class="h-4 w-4" />
          Completed
        </div>
        <div class="text-sm font-semibold text-gray-900">
          {{ formattedCompletedTime }}
        </div>
      </div>

      <div class="bg-white rounded-lg p-3 border border-gray-200">
        <div class="flex items-center gap-2 text-gray-500 text-xs mb-1">
          <ClockIcon class="h-4 w-4" />
          Duration
        </div>
        <div class="text-sm font-semibold text-gray-900">
          {{ formattedDuration }}
        </div>
      </div>

      <div class="bg-white rounded-lg p-3 border border-gray-200">
        <div class="flex items-center gap-2 text-gray-500 text-xs mb-1">
          <ChartBarIcon class="h-4 w-4" />
          Healthy Stocks
        </div>
        <div class="text-sm font-semibold text-gray-900">
          {{ store.healthyStocksCount || '0' }} (F-Score ≥ 7)
        </div>
      </div>
    </div>

    <!-- Recent Updates -->
    <div v-if="recentUpdates.length > 0" class="mt-4">
      <h4 class="text-sm font-semibold text-gray-700 mb-2">Recent Updates</h4>
      <div class="space-y-1">
        <div
          v-for="(update, index) in recentUpdates"
          :key="index"
          class="flex items-center justify-between text-xs bg-white rounded px-3 py-2 border border-gray-200"
        >
          <div class="flex items-center gap-2">
            <CheckCircleIcon
              v-if="update.status === 'Completed'"
              class="h-4 w-4 text-green-600"
            />
            <XCircleIcon
              v-else-if="update.status === 'Error'"
              class="h-4 w-4 text-red-600"
            />
            <ArrowPathIcon
              v-else
              class="h-4 w-4 text-blue-600 animate-spin"
            />

            <span class="font-medium text-gray-900">{{ update.symbol }}</span>

            <span
              v-if="update.status === 'Error' && update.errorMessage"
              class="text-red-600"
            >
              - {{ update.errorMessage }}
            </span>
            <span
              v-else-if="update.status === 'Completed' && update.recommendationsCount > 0"
              class="text-green-600"
            >
              - {{ update.recommendationsCount }} recommendation{{ update.recommendationsCount > 1 ? 's' : '' }}
            </span>
          </div>

          <div class="flex items-center gap-2 text-gray-500">
            <span v-if="update.metrics?.piotroskiFScore !== undefined">
              F: {{ update.metrics.piotroskiFScore }}
            </span>
            <span v-if="update.metrics?.altmanZScore !== undefined">
              Z: {{ update.metrics.altmanZScore.toFixed(2) }}
            </span>
          </div>
        </div>
      </div>
    </div>

    <!-- Error Message -->
    <div
      v-if="store.scanErrorMessage"
      class="mt-4 p-3 bg-red-100 border border-red-300 rounded-lg"
    >
      <p class="text-sm text-red-800">
        <strong>Error:</strong> {{ store.scanErrorMessage }}
      </p>
    </div>
  </div>
</template>
