<script setup lang="ts">
import { computed } from 'vue'
import { Doughnut } from 'vue-chartjs'
import {
  Chart as ChartJS,
  Title,
  Tooltip,
  Legend,
  ArcElement
} from 'chart.js'
import type { PutRecommendation } from '@/types'

ChartJS.register(Title, Tooltip, Legend, ArcElement)

const props = defineProps<{
  recommendations: PutRecommendation[]
}>()

const chartData = computed(() => {
  // Categorize by strategy
  const strategyData = new Map<string, number>()

  for (const rec of props.recommendations) {
    const strategy = rec.strategyName || 'Other'
    strategyData.set(strategy, (strategyData.get(strategy) || 0) + 1)
  }

  const labels = Array.from(strategyData.keys())
  const data = Array.from(strategyData.values())

  const colors = [
    'rgba(59, 130, 246, 0.8)',   // Blue
    'rgba(34, 197, 94, 0.8)',    // Green
    'rgba(168, 85, 247, 0.8)',   // Purple
    'rgba(251, 191, 36, 0.8)',   // Yellow
    'rgba(239, 68, 68, 0.8)'     // Red
  ]

  return {
    labels,
    datasets: [
      {
        data,
        backgroundColor: colors.slice(0, labels.length),
        borderColor: colors.slice(0, labels.length).map(c => c.replace('0.8', '1')),
        borderWidth: 2
      }
    ]
  }
})

const chartOptions = {
  responsive: true,
  maintainAspectRatio: false,
  plugins: {
    legend: {
      position: 'bottom' as const,
      labels: {
        padding: 20
      }
    },
    title: {
      display: true,
      text: 'Recommendations by Strategy',
      font: {
        size: 16
      }
    }
  }
}

// Summary stats
const summaryStats = computed(() => {
  if (props.recommendations.length === 0) {
    return { avgReturn: 0, avgConfidence: 0, totalPremium: 0 }
  }

  let totalReturn = 0
  let totalConfidence = 0
  let totalPremium = 0

  for (const rec of props.recommendations) {
    // Calculate annualized return
    const returnOnRisk = (rec.premium ?? 0) / (rec.strikePrice ?? 1)
    const annualizedReturn = (returnOnRisk * 365) / (rec.daysToExpiry || 1)
    totalReturn += annualizedReturn
    totalConfidence += (rec.confidence ?? 0)
    totalPremium += (rec.premium ?? 0)
  }

  return {
    avgReturn: (totalReturn / props.recommendations.length) * 100,
    avgConfidence: (totalConfidence / props.recommendations.length) * 100,
    totalPremium
  }
})
</script>

<template>
  <div class="bg-white rounded-lg shadow p-6">
    <!-- Chart -->
    <div class="h-64 mb-6">
      <Doughnut
        v-if="recommendations.length > 0"
        :data="chartData"
        :options="chartOptions"
      />
      <div v-else class="flex items-center justify-center h-full text-gray-500">
        No data available
      </div>
    </div>

    <!-- Summary Stats -->
    <div class="grid grid-cols-3 gap-4 pt-4 border-t border-gray-200">
      <div class="text-center">
        <p class="text-2xl font-bold text-green-600">
          {{ summaryStats.avgReturn.toFixed(1) }}%
        </p>
        <p class="text-xs text-gray-500">Avg Annualized Return</p>
      </div>
      <div class="text-center">
        <p class="text-2xl font-bold text-blue-600">
          {{ summaryStats.avgConfidence.toFixed(0) }}%
        </p>
        <p class="text-xs text-gray-500">Avg Confidence</p>
      </div>
      <div class="text-center">
        <p class="text-2xl font-bold text-purple-600">
          ${{ summaryStats.totalPremium.toFixed(2) }}
        </p>
        <p class="text-xs text-gray-500">Total Premium</p>
      </div>
    </div>
  </div>
</template>
