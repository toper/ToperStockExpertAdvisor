<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRecommendationsStore } from '@/stores/recommendations'
import AppLayout from '@/components/layout/AppLayout.vue'
import StatsCards from '@/components/recommendations/StatsCards.vue'
import SymbolFilter from '@/components/recommendations/SymbolFilter.vue'
import RecommendationsTable from '@/components/recommendations/RecommendationsTable.vue'
import RecommendationsList from '@/components/recommendations/RecommendationsList.vue'
import LoadingSpinner from '@/components/common/LoadingSpinner.vue'
import ErrorAlert from '@/components/common/ErrorAlert.vue'
import { Squares2X2Icon, TableCellsIcon, ArrowPathIcon } from '@heroicons/vue/24/outline'

const store = useRecommendationsStore()
const viewMode = ref<'table' | 'cards'>('table')

onMounted(() => {
  store.fetchActiveRecommendations()
})

function refresh() {
  store.fetchActiveRecommendations()
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

      <StatsCards
        :recommendations="store.recommendations"
        :loading="store.loading"
      />

      <div class="bg-white rounded-xl shadow-sm border border-gray-200 p-4">
        <SymbolFilter
          :symbols="store.uniqueSymbols"
          :selected-symbol="store.selectedSymbol"
          @select="store.setSelectedSymbol"
        />
      </div>

      <div v-if="store.loading" class="py-12">
        <LoadingSpinner size="lg" />
        <p class="text-center text-gray-500 mt-4">Loading recommendations...</p>
      </div>

      <template v-else>
        <RecommendationsTable
          v-if="viewMode === 'table'"
          :recommendations="store.filteredRecommendations"
        />
        <RecommendationsList
          v-else
          :recommendations="store.filteredRecommendations"
        />
      </template>

      <div v-if="!store.loading && store.totalCount > 0" class="text-center text-sm text-gray-500">
        Showing {{ store.filteredRecommendations.length }} of {{ store.totalCount }} recommendations
      </div>
    </div>
  </AppLayout>
</template>
