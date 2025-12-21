<script setup lang="ts">
import { format } from 'date-fns'
import { CalendarIcon, ArrowTrendingUpIcon } from '@heroicons/vue/24/outline'
import type { PutRecommendation } from '@/types'

const props = defineProps<{
  recommendation: PutRecommendation
}>()

const confidenceColor = computed(() => {
  const conf = props.recommendation.confidence
  if (conf >= 0.8) return 'text-green-600 bg-green-50 border-green-200'
  if (conf >= 0.7) return 'text-blue-600 bg-blue-50 border-blue-200'
  if (conf >= 0.6) return 'text-yellow-600 bg-yellow-50 border-yellow-200'
  return 'text-gray-600 bg-gray-50 border-gray-200'
})

const formattedExpiry = computed(() => {
  return format(new Date(props.recommendation.expiry), 'MMM dd, yyyy')
})

const formattedScannedAt = computed(() => {
  return format(new Date(props.recommendation.scannedAt), 'MMM dd, HH:mm')
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
        {{ (recommendation.confidence * 100).toFixed(0) }}%
      </span>
    </div>

    <div class="grid grid-cols-2 gap-4 mb-4">
      <div>
        <p class="text-xs text-gray-500 uppercase tracking-wide">Current Price</p>
        <p class="text-lg font-semibold text-gray-900">${{ recommendation.currentPrice.toFixed(2) }}</p>
      </div>
      <div>
        <p class="text-xs text-gray-500 uppercase tracking-wide">Strike Price</p>
        <p class="text-lg font-semibold text-gray-900">${{ recommendation.strikePrice.toFixed(2) }}</p>
      </div>
      <div>
        <p class="text-xs text-gray-500 uppercase tracking-wide">Premium</p>
        <p class="text-lg font-semibold text-green-600">${{ recommendation.premium.toFixed(2) }}</p>
      </div>
      <div>
        <p class="text-xs text-gray-500 uppercase tracking-wide">Breakeven</p>
        <p class="text-lg font-semibold text-gray-900">${{ recommendation.breakeven.toFixed(2) }}</p>
      </div>
    </div>

    <div class="flex items-center gap-4 pt-4 border-t border-gray-100">
      <div class="flex items-center gap-1 text-sm text-gray-600">
        <CalendarIcon class="h-4 w-4" />
        <span>{{ formattedExpiry }}</span>
        <span class="text-gray-400">({{ recommendation.daysToExpiry }}d)</span>
      </div>
      <div class="flex items-center gap-1 text-sm text-green-600">
        <ArrowTrendingUpIcon class="h-4 w-4" />
        <span>+{{ recommendation.expectedGrowthPercent.toFixed(1) }}%</span>
      </div>
    </div>

    <div class="mt-3 pt-3 border-t border-gray-100">
      <div class="flex justify-between text-xs text-gray-500">
        <span>OTM: {{ recommendation.otmPercent.toFixed(1) }}%</span>
        <span>Return: {{ recommendation.potentialReturn.toFixed(2) }}%</span>
        <span>Scanned: {{ formattedScannedAt }}</span>
      </div>
    </div>
  </div>
</template>
