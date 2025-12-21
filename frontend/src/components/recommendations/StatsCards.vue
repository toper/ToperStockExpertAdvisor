<script setup lang="ts">
import {
  DocumentChartBarIcon,
  ArrowTrendingUpIcon,
  ClockIcon,
  CheckCircleIcon
} from '@heroicons/vue/24/outline'
import type { PutRecommendation } from '@/types'

const props = defineProps<{
  recommendations: PutRecommendation[]
  loading: boolean
}>()

const stats = computed(() => {
  const recs = props.recommendations
  if (!recs.length) {
    return {
      total: 0,
      avgConfidence: 0,
      avgDaysToExpiry: 0,
      highConfidence: 0
    }
  }

  return {
    total: recs.length,
    avgConfidence: (recs.reduce((sum, r) => sum + r.confidence, 0) / recs.length * 100).toFixed(1),
    avgDaysToExpiry: Math.round(recs.reduce((sum, r) => sum + r.daysToExpiry, 0) / recs.length),
    highConfidence: recs.filter(r => r.confidence >= 0.7).length
  }
})

import { computed } from 'vue'
</script>

<template>
  <div class="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
    <div class="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
      <div class="flex items-center">
        <div class="flex-shrink-0">
          <DocumentChartBarIcon class="h-8 w-8 text-primary-600" />
        </div>
        <div class="ml-4">
          <p class="text-sm font-medium text-gray-500">Total Recommendations</p>
          <p class="text-2xl font-bold text-gray-900">
            <span v-if="loading" class="animate-pulse">...</span>
            <span v-else>{{ stats.total }}</span>
          </p>
        </div>
      </div>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
      <div class="flex items-center">
        <div class="flex-shrink-0">
          <ArrowTrendingUpIcon class="h-8 w-8 text-green-600" />
        </div>
        <div class="ml-4">
          <p class="text-sm font-medium text-gray-500">Avg Confidence</p>
          <p class="text-2xl font-bold text-gray-900">
            <span v-if="loading" class="animate-pulse">...</span>
            <span v-else>{{ stats.avgConfidence }}%</span>
          </p>
        </div>
      </div>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
      <div class="flex items-center">
        <div class="flex-shrink-0">
          <ClockIcon class="h-8 w-8 text-yellow-600" />
        </div>
        <div class="ml-4">
          <p class="text-sm font-medium text-gray-500">Avg Days to Expiry</p>
          <p class="text-2xl font-bold text-gray-900">
            <span v-if="loading" class="animate-pulse">...</span>
            <span v-else>{{ stats.avgDaysToExpiry }} days</span>
          </p>
        </div>
      </div>
    </div>

    <div class="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
      <div class="flex items-center">
        <div class="flex-shrink-0">
          <CheckCircleIcon class="h-8 w-8 text-purple-600" />
        </div>
        <div class="ml-4">
          <p class="text-sm font-medium text-gray-500">High Confidence</p>
          <p class="text-2xl font-bold text-gray-900">
            <span v-if="loading" class="animate-pulse">...</span>
            <span v-else>{{ stats.highConfidence }}</span>
          </p>
        </div>
      </div>
    </div>
  </div>
</template>
