<script setup lang="ts">
import { format } from 'date-fns'
import { ArrowUpIcon, ArrowDownIcon, CalculatorIcon } from '@heroicons/vue/24/solid'
import type { PutRecommendation } from '@/types'
import { ref, computed } from 'vue'
import Modal from '@/components/common/Modal.vue'
import ProfitCalculator from '@/components/charts/ProfitCalculator.vue'

const props = defineProps<{
  recommendations: PutRecommendation[]
}>()

type SortKey = 'symbol' | 'confidence' | 'daysToExpiry' | 'premium' | 'expectedGrowthPercent'
type SortDirection = 'asc' | 'desc'

const sortKey = ref<SortKey>('confidence')
const sortDirection = ref<SortDirection>('desc')
const showCalculator = ref(false)
const selectedRecommendation = ref<PutRecommendation | null>(null)

const sortedRecommendations = computed(() => {
  return [...props.recommendations].sort((a, b) => {
    const aVal = a[sortKey.value]
    const bVal = b[sortKey.value]
    const modifier = sortDirection.value === 'asc' ? 1 : -1

    if (typeof aVal === 'string' && typeof bVal === 'string') {
      return aVal.localeCompare(bVal) * modifier
    }
    return ((aVal as number) - (bVal as number)) * modifier
  })
})

function toggleSort(key: SortKey) {
  if (sortKey.value === key) {
    sortDirection.value = sortDirection.value === 'asc' ? 'desc' : 'asc'
  } else {
    sortKey.value = key
    sortDirection.value = 'desc'
  }
}

function getConfidenceClass(confidence: number): string {
  if (confidence >= 0.8) return 'bg-green-100 text-green-800'
  if (confidence >= 0.7) return 'bg-blue-100 text-blue-800'
  if (confidence >= 0.6) return 'bg-yellow-100 text-yellow-800'
  return 'bg-gray-100 text-gray-800'
}

function formatDate(dateStr: string): string {
  return format(new Date(dateStr), 'MMM dd, yyyy')
}

function openCalculator(recommendation: PutRecommendation) {
  selectedRecommendation.value = recommendation
  showCalculator.value = true
}

function closeCalculator() {
  showCalculator.value = false
  selectedRecommendation.value = null
}
</script>

<template>
  <div class="bg-white rounded-xl shadow-sm border border-gray-200 overflow-hidden">
    <div class="overflow-x-auto">
      <table class="min-w-full divide-y divide-gray-200">
        <thead class="bg-gray-50">
          <tr>
            <th
              @click="toggleSort('symbol')"
              class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider cursor-pointer hover:bg-gray-100"
            >
              <div class="flex items-center gap-1">
                Symbol
                <component
                  v-if="sortKey === 'symbol'"
                  :is="sortDirection === 'asc' ? ArrowUpIcon : ArrowDownIcon"
                  class="h-3 w-3"
                />
              </div>
            </th>
            <th class="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
              Strategy
            </th>
            <th class="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">
              Current
            </th>
            <th class="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">
              Strike
            </th>
            <th
              @click="toggleSort('premium')"
              class="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider cursor-pointer hover:bg-gray-100"
            >
              <div class="flex items-center justify-end gap-1">
                Premium
                <component
                  v-if="sortKey === 'premium'"
                  :is="sortDirection === 'asc' ? ArrowUpIcon : ArrowDownIcon"
                  class="h-3 w-3"
                />
              </div>
            </th>
            <th
              @click="toggleSort('daysToExpiry')"
              class="px-6 py-3 text-center text-xs font-medium text-gray-500 uppercase tracking-wider cursor-pointer hover:bg-gray-100"
            >
              <div class="flex items-center justify-center gap-1">
                Expiry
                <component
                  v-if="sortKey === 'daysToExpiry'"
                  :is="sortDirection === 'asc' ? ArrowUpIcon : ArrowDownIcon"
                  class="h-3 w-3"
                />
              </div>
            </th>
            <th
              @click="toggleSort('expectedGrowthPercent')"
              class="px-6 py-3 text-right text-xs font-medium text-gray-500 uppercase tracking-wider cursor-pointer hover:bg-gray-100"
            >
              <div class="flex items-center justify-end gap-1">
                Growth
                <component
                  v-if="sortKey === 'expectedGrowthPercent'"
                  :is="sortDirection === 'asc' ? ArrowUpIcon : ArrowDownIcon"
                  class="h-3 w-3"
                />
              </div>
            </th>
            <th
              @click="toggleSort('confidence')"
              class="px-6 py-3 text-center text-xs font-medium text-gray-500 uppercase tracking-wider cursor-pointer hover:bg-gray-100"
            >
              <div class="flex items-center justify-center gap-1">
                Confidence
                <component
                  v-if="sortKey === 'confidence'"
                  :is="sortDirection === 'asc' ? ArrowUpIcon : ArrowDownIcon"
                  class="h-3 w-3"
                />
              </div>
            </th>
            <th class="px-6 py-3 text-center text-xs font-medium text-gray-500 uppercase tracking-wider">
              Actions
            </th>
          </tr>
        </thead>
        <tbody class="bg-white divide-y divide-gray-200">
          <tr
            v-for="rec in sortedRecommendations"
            :key="rec.id"
            class="hover:bg-gray-50 transition-colors"
          >
            <td class="px-6 py-4 whitespace-nowrap">
              <span class="text-sm font-bold text-gray-900">{{ rec.symbol }}</span>
            </td>
            <td class="px-6 py-4 whitespace-nowrap">
              <span class="text-sm text-gray-600">{{ rec.strategyName }}</span>
            </td>
            <td class="px-6 py-4 whitespace-nowrap text-right">
              <span class="text-sm text-gray-900">${{ rec.currentPrice.toFixed(2) }}</span>
            </td>
            <td class="px-6 py-4 whitespace-nowrap text-right">
              <span class="text-sm text-gray-900">${{ rec.strikePrice.toFixed(2) }}</span>
            </td>
            <td class="px-6 py-4 whitespace-nowrap text-right">
              <span class="text-sm font-medium text-green-600">${{ rec.premium.toFixed(2) }}</span>
            </td>
            <td class="px-6 py-4 whitespace-nowrap text-center">
              <div class="text-sm text-gray-900">{{ formatDate(rec.expiry) }}</div>
              <div class="text-xs text-gray-500">{{ rec.daysToExpiry }}d</div>
            </td>
            <td class="px-6 py-4 whitespace-nowrap text-right">
              <span class="text-sm font-medium text-green-600">+{{ rec.expectedGrowthPercent.toFixed(1) }}%</span>
            </td>
            <td class="px-6 py-4 whitespace-nowrap text-center">
              <span
                :class="[
                  'inline-flex px-2 py-1 text-xs font-semibold rounded-full',
                  getConfidenceClass(rec.confidence)
                ]"
              >
                {{ (rec.confidence * 100).toFixed(0) }}%
              </span>
            </td>
            <td class="px-6 py-4 whitespace-nowrap text-center">
              <button
                @click="openCalculator(rec)"
                class="inline-flex items-center gap-1 px-3 py-1.5 bg-blue-600 text-white text-xs font-medium rounded-lg hover:bg-blue-700 transition-colors"
                title="Calculate profit/loss"
              >
                <CalculatorIcon class="h-4 w-4" />
                Calculate
              </button>
            </td>
          </tr>
          <tr v-if="!recommendations.length">
            <td colspan="9" class="px-6 py-12 text-center text-gray-500">
              No recommendations found
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Calculator Modal -->
    <Modal
      :open="showCalculator"
      :title="`Profit Calculator - ${selectedRecommendation?.symbol || ''}`"
      @close="closeCalculator"
    >
      <ProfitCalculator
        v-if="selectedRecommendation"
        :recommendation="selectedRecommendation"
      />
    </Modal>
  </div>
</template>
