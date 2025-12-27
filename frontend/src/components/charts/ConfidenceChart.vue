<script setup lang="ts">
import { computed } from 'vue'
import { Bar } from 'vue-chartjs'
import {
  Chart as ChartJS,
  Title,
  Tooltip,
  Legend,
  BarElement,
  CategoryScale,
  LinearScale
} from 'chart.js'
import type { PutRecommendation } from '@/types'

ChartJS.register(Title, Tooltip, Legend, BarElement, CategoryScale, LinearScale)

const props = defineProps<{
  recommendations: PutRecommendation[]
}>()

const chartData = computed(() => {
  // Group by symbol and get average confidence
  const symbolConfidence = new Map<string, { total: number; count: number }>()

  for (const rec of props.recommendations) {
    const existing = symbolConfidence.get(rec.symbol) || { total: 0, count: 0 }
    symbolConfidence.set(rec.symbol, {
      total: existing.total + rec.confidence,
      count: existing.count + 1
    })
  }

  const labels: string[] = []
  const data: number[] = []
  const backgroundColors: string[] = []

  for (const [symbol, { total, count }] of symbolConfidence) {
    const avgConfidence = (total / count) * 100
    labels.push(symbol)
    data.push(Math.round(avgConfidence))

    // Color based on confidence level
    if (avgConfidence >= 80) {
      backgroundColors.push('rgba(34, 197, 94, 0.8)') // Green
    } else if (avgConfidence >= 70) {
      backgroundColors.push('rgba(59, 130, 246, 0.8)') // Blue
    } else if (avgConfidence >= 60) {
      backgroundColors.push('rgba(251, 191, 36, 0.8)') // Yellow
    } else {
      backgroundColors.push('rgba(239, 68, 68, 0.8)') // Red
    }
  }

  return {
    labels,
    datasets: [
      {
        label: 'Confidence Score (%)',
        data,
        backgroundColor: backgroundColors,
        borderWidth: 1,
        borderRadius: 4
      }
    ]
  }
})

const chartOptions = {
  responsive: true,
  maintainAspectRatio: false,
  plugins: {
    legend: {
      display: false
    },
    title: {
      display: true,
      text: 'Confidence by Symbol',
      font: {
        size: 16
      }
    },
    tooltip: {
      callbacks: {
        label: (context: any) => `Confidence: ${context.parsed.y}%`
      }
    }
  },
  scales: {
    y: {
      beginAtZero: true,
      max: 100,
      ticks: {
        callback: (value: string | number) => `${value}%`
      }
    }
  }
}
</script>

<template>
  <div class="bg-white rounded-lg shadow p-6">
    <div class="h-64">
      <Bar
        v-if="recommendations.length > 0"
        :data="chartData"
        :options="chartOptions"
      />
      <div v-else class="flex items-center justify-center h-full text-gray-500">
        No data available
      </div>
    </div>
  </div>
</template>
