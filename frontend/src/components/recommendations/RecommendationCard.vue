<script setup lang="ts">
import { format } from 'date-fns'
import { CalendarIcon, ArrowTrendingUpIcon, CalculatorIcon, InformationCircleIcon } from '@heroicons/vue/24/outline'
import type { PutRecommendation } from '@/types'

const props = defineProps<{
  recommendation: PutRecommendation
}>()

const emit = defineEmits<{
  calculate: [recommendation: PutRecommendation]
  viewDetails: [recommendation: PutRecommendation]
}>()

const confidenceColor = computed(() => {
  const conf = props.recommendation.confidence ?? 0
  if (conf >= 0.8) return 'text-green-600 bg-green-50 border-green-200'
  if (conf >= 0.7) return 'text-blue-600 bg-blue-50 border-blue-200'
  if (conf >= 0.6) return 'text-yellow-600 bg-yellow-50 border-yellow-200'
  return 'text-gray-600 bg-gray-50 border-gray-200'
})

const formattedExpiry = computed(() => {
  return props.recommendation.expiry ? format(new Date(props.recommendation.expiry), 'MMM dd, yyyy') : '-'
})

const formattedModificationTime = computed(() => {
  return format(new Date(props.recommendation.modificationTime), 'MMM dd, HH:mm')
})

import { computed } from 'vue'
</script>

<template>
  <div class="bg-white rounded-xl shadow-sm border border-gray-200 p-6 hover:shadow-md transition-shadow">
    <div class="flex justify-between items-start mb-4">
      <div>
        <h3 class="text-xl font-bold text-gray-900">{{ recommendation.symbol }}</h3>
        <p class="text-sm text-gray-500">{{ recommendation.strategyName }}</p>
      </div>
      <span
        :class="[
          'px-3 py-1 rounded-full text-sm font-semibold border',
          confidenceColor
        ]"
      >
        {{ ((recommendation.confidence ?? 0) * 100).toFixed(0) }}%
      </span>
    </div>

    <div class="grid grid-cols-2 gap-4 mb-4">
      <div>
        <p class="text-xs text-gray-500 uppercase tracking-wide">Current Price</p>
        <p class="text-lg font-semibold text-gray-900">${{ (recommendation.currentPrice ?? 0).toFixed(2) }}</p>
      </div>
      <div>
        <p class="text-xs text-gray-500 uppercase tracking-wide">Strike Price</p>
        <p class="text-lg font-semibold text-gray-900">${{ (recommendation.strikePrice ?? 0).toFixed(2) }}</p>
      </div>
      <div>
        <p class="text-xs text-gray-500 uppercase tracking-wide">Premium</p>
        <p class="text-lg font-semibold text-green-600">${{ (recommendation.premium ?? 0).toFixed(2) }}</p>
      </div>
      <div>
        <p class="text-xs text-gray-500 uppercase tracking-wide">Breakeven</p>
        <p class="text-lg font-semibold text-gray-900">${{ (recommendation.breakeven ?? 0).toFixed(2) }}</p>
      </div>
      <div>
        <p class="text-xs text-gray-500 uppercase tracking-wide">F-Score</p>
        <p class="text-lg font-semibold text-gray-900">{{ recommendation.piotroskiFScore ?? '-' }}</p>
      </div>
      <div>
        <p class="text-xs text-gray-500 uppercase tracking-wide">Z-Score</p>
        <p class="text-lg font-semibold text-gray-900">{{ recommendation.altmanZScore?.toFixed(2) ?? '-' }}</p>
      </div>
    </div>

    <div v-if="recommendation.exanteSymbol" class="mb-4 p-3 bg-blue-50 rounded-lg border border-blue-200">
      <p class="text-xs text-blue-600 uppercase tracking-wide mb-1">Exante Symbol</p>
      <p class="text-sm font-mono text-blue-900">{{ recommendation.exanteSymbol }}</p>
    </div>

    <div class="grid grid-cols-3 gap-4 mb-4">
      <div>
        <p class="text-xs text-gray-500 uppercase tracking-wide">Option Price</p>
        <p class="text-lg font-semibold text-blue-600">{{ recommendation.optionPrice ? '$' + recommendation.optionPrice.toFixed(2) : '-' }}</p>
      </div>
      <div>
        <p class="text-xs text-gray-500 uppercase tracking-wide">Volume</p>
        <p class="text-lg font-semibold text-gray-900">{{ recommendation.volume?.toLocaleString() ?? '-' }}</p>
      </div>
      <div>
        <p class="text-xs text-gray-500 uppercase tracking-wide">Open Interest</p>
        <p class="text-lg font-semibold text-gray-900">{{ recommendation.openInterest?.toLocaleString() ?? '-' }}</p>
      </div>
    </div>

    <div class="flex items-center gap-4 pt-4 border-t border-gray-100">
      <div class="flex items-center gap-1 text-sm text-gray-600">
        <CalendarIcon class="h-4 w-4" />
        <span>{{ formattedExpiry }}</span>
        <span class="text-gray-400">({{ recommendation.daysToExpiry ?? 0 }}d)</span>
      </div>
      <div class="flex items-center gap-1 text-sm text-green-600">
        <ArrowTrendingUpIcon class="h-4 w-4" />
        <span>+{{ (recommendation.expectedGrowthPercent ?? 0).toFixed(1) }}%</span>
      </div>
    </div>

    <div class="mt-3 pt-3 border-t border-gray-100">
      <div class="flex justify-between text-xs text-gray-500">
        <span>OTM: {{ recommendation.otmPercent.toFixed(1) }}%</span>
        <span>Return: {{ recommendation.potentialReturn.toFixed(2) }}%</span>
        <span>Updated: {{ formattedModificationTime }}</span>
      </div>
    </div>

    <div class="mt-4 grid grid-cols-2 gap-2">
      <button
        @click="emit('calculate', recommendation)"
        class="inline-flex items-center justify-center gap-2 px-3 py-2 bg-blue-600 text-white text-sm font-medium rounded-lg hover:bg-blue-700 transition-colors"
      >
        <CalculatorIcon class="h-5 w-5" />
        Kalkulator
      </button>
      <button
        @click="emit('viewDetails', recommendation)"
        class="inline-flex items-center justify-center gap-2 px-3 py-2 bg-purple-600 text-white text-sm font-medium rounded-lg hover:bg-purple-700 transition-colors"
      >
        <InformationCircleIcon class="h-5 w-5" />
        Szczegóły
      </button>
    </div>
  </div>
</template>
