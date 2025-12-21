import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { PutRecommendation } from '@/types'
import { getRecommendations, getActiveRecommendations, getRecommendationsBySymbol } from '@/api/recommendations'

export const useRecommendationsStore = defineStore('recommendations', () => {
  const recommendations = ref<PutRecommendation[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)
  const totalCount = ref(0)
  const selectedSymbol = ref<string | null>(null)

  const sortedRecommendations = computed(() => {
    return [...recommendations.value].sort((a, b) => b.confidence - a.confidence)
  })

  const uniqueSymbols = computed(() => {
    const symbols = new Set(recommendations.value.map(r => r.symbol))
    return Array.from(symbols).sort()
  })

  const filteredRecommendations = computed(() => {
    if (!selectedSymbol.value) return sortedRecommendations.value
    return sortedRecommendations.value.filter(r => r.symbol === selectedSymbol.value)
  })

  const highConfidenceCount = computed(() => {
    return recommendations.value.filter(r => r.confidence >= 0.7).length
  })

  async function fetchRecommendations(minDays?: number, maxDays?: number) {
    loading.value = true
    error.value = null
    try {
      const result = await getRecommendations({ minDays, maxDays })
      if (result.isValid && result.data) {
        recommendations.value = result.data
      } else {
        error.value = result.errors?.join(', ') || 'Failed to fetch recommendations'
      }
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Unknown error'
    } finally {
      loading.value = false
    }
  }

  async function fetchActiveRecommendations() {
    loading.value = true
    error.value = null
    try {
      const result = await getActiveRecommendations()
      if (result.isValid && result.data) {
        recommendations.value = result.data
        totalCount.value = result.totalCount
      } else {
        error.value = result.errors?.join(', ') || 'Failed to fetch recommendations'
      }
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Unknown error'
    } finally {
      loading.value = false
    }
  }

  async function fetchBySymbol(symbol: string) {
    loading.value = true
    error.value = null
    try {
      const result = await getRecommendationsBySymbol(symbol)
      if (result.isValid && result.data) {
        recommendations.value = result.data
      } else {
        error.value = result.errors?.join(', ') || 'Failed to fetch recommendations'
      }
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Unknown error'
    } finally {
      loading.value = false
    }
  }

  function setSelectedSymbol(symbol: string | null) {
    selectedSymbol.value = symbol
  }

  function clearError() {
    error.value = null
  }

  return {
    recommendations,
    loading,
    error,
    totalCount,
    selectedSymbol,
    sortedRecommendations,
    uniqueSymbols,
    filteredRecommendations,
    highConfidenceCount,
    fetchRecommendations,
    fetchActiveRecommendations,
    fetchBySymbol,
    setSelectedSymbol,
    clearError
  }
})
