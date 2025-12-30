<script setup lang="ts">
import { computed } from 'vue'
import type { PutRecommendation } from '@/types'

const props = defineProps<{
  recommendations: PutRecommendation[]
  selectedStrategy: string | null
}>()

const emit = defineEmits<{
  (e: 'update:selectedStrategy', value: string | null): void
}>()

const strategies = computed(() => {
  const strategySet = new Set<string>()
  for (const rec of props.recommendations) {
    if (rec.strategyName) {
      strategySet.add(rec.strategyName)
    }
  }
  return Array.from(strategySet).sort()
})

const strategyStats = computed(() => {
  const stats = new Map<string, { count: number; avgConfidence: number }>()

  for (const rec of props.recommendations) {
    const strategy = rec.strategyName || 'Unknown'
    const existing = stats.get(strategy) || { count: 0, avgConfidence: 0 }
    stats.set(strategy, {
      count: existing.count + 1,
      avgConfidence: (existing.avgConfidence * existing.count + (rec.confidence ?? 0)) / (existing.count + 1)
    })
  }

  return stats
})

function selectStrategy(strategy: string | null) {
  emit('update:selectedStrategy', strategy)
}

function getStrategyDescription(strategy: string): string {
  const descriptions: Record<string, string> = {
    'ShortTermPut': 'Aggressive strategy targeting 2-3 week expirations on trending stocks',
    'DividendMomentum': 'Conservative strategy focusing on dividend-paying stocks with momentum',
    'VolatilityCrush': 'Captures premium from elevated IV expected to decrease'
  }
  return descriptions[strategy] || 'Custom strategy'
}

function getStrategyColor(strategy: string): string {
  const colors: Record<string, string> = {
    'ShortTermPut': 'bg-blue-100 text-blue-800 border-blue-200',
    'DividendMomentum': 'bg-green-100 text-green-800 border-green-200',
    'VolatilityCrush': 'bg-purple-100 text-purple-800 border-purple-200'
  }
  return colors[strategy] || 'bg-gray-100 text-gray-800 border-gray-200'
}
</script>

<template>
  <div class="bg-white rounded-xl shadow-sm border border-gray-200 p-4">
    <h3 class="text-sm font-semibold text-gray-700 mb-3">Filter by Strategy</h3>

    <div class="space-y-2">
      <!-- All Strategies Option -->
      <button
        @click="selectStrategy(null)"
        :class="[
          'w-full text-left px-4 py-3 rounded-lg border transition-all',
          selectedStrategy === null
            ? 'bg-gray-800 text-white border-gray-800'
            : 'bg-gray-50 text-gray-700 border-gray-200 hover:bg-gray-100'
        ]"
      >
        <div class="flex justify-between items-center">
          <span class="font-medium">All Strategies</span>
          <span class="text-sm">{{ recommendations.length }} recommendations</span>
        </div>
      </button>

      <!-- Individual Strategies -->
      <button
        v-for="strategy in strategies"
        :key="strategy"
        @click="selectStrategy(strategy)"
        :class="[
          'w-full text-left px-4 py-3 rounded-lg border transition-all',
          selectedStrategy === strategy
            ? getStrategyColor(strategy) + ' ring-2 ring-offset-1'
            : 'bg-white text-gray-700 border-gray-200 hover:bg-gray-50'
        ]"
      >
        <div class="flex justify-between items-center mb-1">
          <span class="font-medium">{{ strategy }}</span>
          <span class="text-sm">
            {{ strategyStats.get(strategy)?.count || 0 }} recs
          </span>
        </div>
        <p class="text-xs text-gray-500">
          {{ getStrategyDescription(strategy) }}
        </p>
        <div class="mt-2 flex items-center text-xs">
          <span class="text-gray-400">Avg Confidence:</span>
          <span class="ml-1 font-medium">
            {{ ((strategyStats.get(strategy)?.avgConfidence || 0) * 100).toFixed(0) }}%
          </span>
        </div>
      </button>
    </div>
  </div>
</template>
