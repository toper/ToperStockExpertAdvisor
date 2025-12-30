<script setup lang="ts">
import { onMounted, onUnmounted, ref, computed } from 'vue'
import { useRecommendationsStore } from '@/stores/recommendations'
import AppLayout from '@/components/layout/AppLayout.vue'
import StatsCards from '@/components/recommendations/StatsCards.vue'
import StrategySelector from '@/components/recommendations/StrategySelector.vue'
import DateFilter from '@/components/recommendations/DateFilter.vue'
import RecommendationsTable from '@/components/recommendations/RecommendationsTable.vue'
import RecommendationsList from '@/components/recommendations/RecommendationsList.vue'
import ScanProgress from '@/components/recommendations/ScanProgress.vue'
import ScanNotificationToast from '@/components/recommendations/ScanNotificationToast.vue'
import LoadingSpinner from '@/components/common/LoadingSpinner.vue'
import ErrorAlert from '@/components/common/ErrorAlert.vue'
import { Squares2X2Icon, TableCellsIcon, ArrowPathIcon } from '@heroicons/vue/24/outline'

const store = useRecommendationsStore()
const viewMode = ref<'table' | 'cards'>('table')
const selectedStrategy = ref<string | null>(null)
const dateRange = ref<{ from: Date | null; to: Date | null }>({ from: null, to: null })
const filters = ref({
  minConfidence: 0.8,
  minFScore: 7,
  minZScore: 3.0,
  minDays: 14,
  maxDays: 21
})

const filteredRecommendations = computed(() => {
  let filtered = store.sortedRecommendations

  // Filter by strategy
  if (selectedStrategy.value) {
    filtered = filtered.filter(r => r.strategyName === selectedStrategy.value)
  }

  // Filter by modification date
  if (dateRange.value.from || dateRange.value.to) {
    filtered = filtered.filter(r => {
      const modifiedDate = new Date(r.modificationTime)
      if (dateRange.value.from && modifiedDate < dateRange.value.from) return false
      if (dateRange.value.to && modifiedDate > dateRange.value.to) return false
      return true
    })
  }

  // Filter by confidence
  if (filters.value.minConfidence > 0) {
    filtered = filtered.filter(r => (r.confidence ?? 0) >= filters.value.minConfidence)
  }

  // Filter by F-Score
  if (filters.value.minFScore > 0 && filtered.length > 0) {
    filtered = filtered.filter(r => (r.piotroskiFScore ?? 0) >= filters.value.minFScore)
  }

  // Filter by Z-Score
  if (filters.value.minZScore > 0 && filtered.length > 0) {
    filtered = filtered.filter(r => (r.altmanZScore ?? 0) >= filters.value.minZScore)
  }

  // Filter by days to expiry
  if (filters.value.minDays > 0 || filters.value.maxDays > 0) {
    filtered = filtered.filter(r => {
      const days = r.daysToExpiry ?? 0
      if (filters.value.minDays > 0 && days < filters.value.minDays) return false
      if (filters.value.maxDays > 0 && days > filters.value.maxDays) return false
      return true
    })
  }

  return filtered
})

onMounted(async () => {
  // Start SignalR connection for real-time scan updates
  await store.startSignalR()

  // Fetch initial recommendations
  store.fetchActiveRecommendations()
})

onUnmounted(async () => {
  // Clean up SignalR connection
  await store.stopSignalR()
})

function refresh() {
  store.fetchActiveRecommendations()
}

function handleStrategySelect(strategy: string | null) {
  selectedStrategy.value = strategy
}

function handleDateRangeUpdate(range: { from: Date | null; to: Date | null }) {
  dateRange.value = range
}
</script>

<template>
  <AppLayout>
    <div class="space-y-6">
      <div class="flex justify-between items-center">
        <div>
          <h2 class="text-2xl font-bold text-gray-900">PUT Options Recommendations</h2>
          <p class="text-gray-500">Short-term options (14-21 days) with high confidence</p>
        </div>
        <div class="flex items-center gap-2">
          <button
            @click="refresh"
            :disabled="store.loading"
            class="inline-flex items-center gap-2 px-4 py-2 bg-primary-600 text-white rounded-lg hover:bg-primary-700 disabled:opacity-50 transition-colors"
          >
            <ArrowPathIcon :class="['h-4 w-4', store.loading && 'animate-spin']" />
            Refresh
          </button>
          <div class="flex rounded-lg border border-gray-200 bg-white p-1">
            <button
              @click="viewMode = 'table'"
              :class="[
                'p-2 rounded-md transition-colors',
                viewMode === 'table' ? 'bg-primary-100 text-primary-600' : 'text-gray-500 hover:text-gray-700'
              ]"
            >
              <TableCellsIcon class="h-5 w-5" />
            </button>
            <button
              @click="viewMode = 'cards'"
              :class="[
                'p-2 rounded-md transition-colors',
                viewMode === 'cards' ? 'bg-primary-100 text-primary-600' : 'text-gray-500 hover:text-gray-700'
              ]"
            >
              <Squares2X2Icon class="h-5 w-5" />
            </button>
          </div>
        </div>
      </div>

      <ErrorAlert
        v-if="store.error"
        :message="store.error"
        @close="store.clearError"
      />

      <ScanProgress />

      <StatsCards
        :recommendations="store.recommendations"
        :loading="store.loading"
      />

      <div class="grid grid-cols-1 lg:grid-cols-2 gap-4">
        <StrategySelector
          :recommendations="store.recommendations"
          :selectedStrategy="selectedStrategy"
          @update:selectedStrategy="handleStrategySelect"
        />
        <DateFilter
          @update:dateRange="handleDateRangeUpdate"
        />
      </div>

      <!-- Advanced Filters -->
      <div class="bg-white rounded-lg shadow p-6">
        <h3 class="text-lg font-semibold text-gray-900 mb-4">Advanced Filters</h3>
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-5 gap-4">
          <!-- Confidence -->
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-2">
              Min Confidence: {{ (filters.minConfidence * 100).toFixed(0) }}%
            </label>
            <input
              v-model.number="filters.minConfidence"
              type="range"
              min="0"
              max="1"
              step="0.05"
              class="w-full h-2 bg-gray-200 rounded-lg appearance-none cursor-pointer accent-primary-600"
            />
            <div class="flex justify-between text-xs text-gray-500 mt-1">
              <span>0%</span>
              <span>100%</span>
            </div>
          </div>

          <!-- F-Score -->
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-2">
              Min F-Score: {{ filters.minFScore }}
            </label>
            <input
              v-model.number="filters.minFScore"
              type="range"
              min="0"
              max="9"
              step="1"
              class="w-full h-2 bg-gray-200 rounded-lg appearance-none cursor-pointer accent-primary-600"
            />
            <div class="flex justify-between text-xs text-gray-500 mt-1">
              <span>0</span>
              <span>9</span>
            </div>
          </div>

          <!-- Z-Score -->
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-2">
              Min Z-Score: {{ filters.minZScore.toFixed(1) }}
            </label>
            <input
              v-model.number="filters.minZScore"
              type="range"
              min="0"
              max="5"
              step="0.1"
              class="w-full h-2 bg-gray-200 rounded-lg appearance-none cursor-pointer accent-primary-600"
            />
            <div class="flex justify-between text-xs text-gray-500 mt-1">
              <span>0.0</span>
              <span>5.0</span>
            </div>
          </div>

          <!-- Min Days -->
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-2">
              Min Days: {{ filters.minDays }}
            </label>
            <input
              v-model.number="filters.minDays"
              type="range"
              min="0"
              max="90"
              step="1"
              class="w-full h-2 bg-gray-200 rounded-lg appearance-none cursor-pointer accent-primary-600"
            />
            <div class="flex justify-between text-xs text-gray-500 mt-1">
              <span>0</span>
              <span>90</span>
            </div>
          </div>

          <!-- Max Days -->
          <div>
            <label class="block text-sm font-medium text-gray-700 mb-2">
              Max Days: {{ filters.maxDays }}
            </label>
            <input
              v-model.number="filters.maxDays"
              type="range"
              min="0"
              max="90"
              step="1"
              class="w-full h-2 bg-gray-200 rounded-lg appearance-none cursor-pointer accent-primary-600"
            />
            <div class="flex justify-between text-xs text-gray-500 mt-1">
              <span>0</span>
              <span>90</span>
            </div>
          </div>
        </div>

        <!-- Reset button -->
        <div class="mt-4 flex justify-end">
          <button
            @click="filters = { minConfidence: 0.8, minFScore: 7, minZScore: 3.0, minDays: 14, maxDays: 21 }"
            class="px-4 py-2 text-sm font-medium text-gray-700 bg-gray-100 rounded-lg hover:bg-gray-200 transition-colors"
          >
            Reset Filters
          </button>
        </div>
      </div>

      <div v-if="store.loading" class="py-12">
        <LoadingSpinner size="lg" />
        <p class="text-center text-gray-500 mt-4">Loading recommendations...</p>
      </div>

      <template v-else>
        <RecommendationsTable
          v-if="viewMode === 'table'"
          :recommendations="filteredRecommendations"
        />
        <RecommendationsList
          v-else
          :recommendations="filteredRecommendations"
        />
      </template>

      <div v-if="!store.loading && store.totalCount > 0" class="text-center text-sm text-gray-500">
        Showing {{ filteredRecommendations.length }} of {{ store.totalCount }} recommendations
      </div>
    </div>

    <!-- Toast Notifications -->
    <ScanNotificationToast />
  </AppLayout>
</template>
