import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import type { PutRecommendation } from '@/types'
import { getRecommendations, getActiveRecommendations, getRecommendationsBySymbol, getRecommendationsStats } from '@/api/recommendations'
import { createScanProgressHub, type ScanProgressUpdate, type ScanStartedEvent, type ScanCompletedEvent } from '@/services/signalr'

export const useRecommendationsStore = defineStore('recommendations', () => {
  const recommendations = ref<PutRecommendation[]>([])
  const loading = ref(false)
  const error = ref<string | null>(null)
  const totalCount = ref(0)
  const healthyStocksCount = ref(0)

  // Scan progress state
  const scanInProgress = ref(false)
  const scanLogId = ref<number | null>(null)
  const currentScanSymbol = ref<string>('')
  const scannedSymbolsCount = ref(0)
  const totalSymbolsToScan = ref(0)
  const scanProgressPercent = ref(0)
  const scanStartedAt = ref<Date | null>(null)
  const scanCompletedAt = ref<Date | null>(null)
  const scanErrorMessage = ref<string | null>(null)
  const symbolUpdates = ref<ScanProgressUpdate[]>([])
  const scanDuration = ref(0)

  // SignalR hub client
  // In production (Docker), use relative URL (empty string)
  // In development, use localhost:5001
  const apiUrl = import.meta.env.VITE_API_URL ||
    (import.meta.env.MODE === 'production' ? '' : 'http://localhost:5001')
  const hubClient = createScanProgressHub(apiUrl)

  const sortedRecommendations = computed(() => {
    return [...recommendations.value].sort((a, b) => (b.confidence ?? 0) - (a.confidence ?? 0))
  })

  const highConfidenceCount = computed(() => {
    return recommendations.value.filter(r => (r.confidence ?? 0) >= 0.7).length
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

  async function fetchStats() {
    try {
      const stats = await getRecommendationsStats()
      healthyStocksCount.value = stats.healthyStocksCount
    } catch (e) {
      console.error('Failed to fetch stats:', e)
    }
  }

  function clearError() {
    error.value = null
  }

  // SignalR event handlers
  function handleScanStarted(event: ScanStartedEvent) {
    scanInProgress.value = true
    scanLogId.value = event.scanLogId
    totalSymbolsToScan.value = event.totalSymbols
    scannedSymbolsCount.value = 0
    scanProgressPercent.value = 0
    scanStartedAt.value = new Date(event.timestamp)
    scanCompletedAt.value = null
    scanErrorMessage.value = null
    symbolUpdates.value = []
    currentScanSymbol.value = ''
  }

  function handleSymbolScanning(update: ScanProgressUpdate) {
    currentScanSymbol.value = update.symbol
    scannedSymbolsCount.value = update.currentIndex
    scanProgressPercent.value = update.progressPercent
  }

  function handleSymbolCompleted(update: ScanProgressUpdate) {
    symbolUpdates.value.push(update)
    scannedSymbolsCount.value = update.currentIndex + 1
    scanProgressPercent.value = update.progressPercent
  }

  function handleSymbolError(update: ScanProgressUpdate) {
    symbolUpdates.value.push(update)
    scannedSymbolsCount.value = update.currentIndex + 1
    scanProgressPercent.value = update.progressPercent
  }

  function handleScanCompleted(event: ScanCompletedEvent) {
    scanInProgress.value = false
    scanCompletedAt.value = event.completedAt ? new Date(event.completedAt) : null
    scanDuration.value = event.duration
    currentScanSymbol.value = ''

    if (event.status === 'Failed') {
      scanErrorMessage.value = event.errorMessage || 'Scan failed'
    }

    // Refresh recommendations and stats after scan completes
    fetchActiveRecommendations()
    fetchStats()
  }

  // Start SignalR connection
  async function startSignalR() {
    try {
      await hubClient.start({
        onScanStarted: handleScanStarted,
        onSymbolScanning: handleSymbolScanning,
        onSymbolCompleted: handleSymbolCompleted,
        onSymbolError: handleSymbolError,
        onScanCompleted: handleScanCompleted,
        onConnected: () => {
          console.log('Connected to scan progress hub')
        },
        onDisconnected: (err) => {
          console.warn('Disconnected from scan progress hub', err)
        },
        onReconnecting: () => {
          console.log('Reconnecting to scan progress hub...')
        },
        onReconnected: () => {
          console.log('Reconnected to scan progress hub')
        }
      })
    } catch (err) {
      console.error('Failed to start SignalR connection:', err)
    }
  }

  // Stop SignalR connection
  async function stopSignalR() {
    try {
      await hubClient.stop()
    } catch (err) {
      console.error('Failed to stop SignalR connection:', err)
    }
  }

  // Clear scan progress
  function clearScanProgress() {
    scanInProgress.value = false
    scanLogId.value = null
    currentScanSymbol.value = ''
    scannedSymbolsCount.value = 0
    totalSymbolsToScan.value = 0
    scanProgressPercent.value = 0
    scanStartedAt.value = null
    scanCompletedAt.value = null
    scanErrorMessage.value = null
    symbolUpdates.value = []
    scanDuration.value = 0
  }

  return {
    // Existing state
    recommendations,
    loading,
    error,
    totalCount,
    healthyStocksCount,
    sortedRecommendations,
    highConfidenceCount,

    // Scan progress state
    scanInProgress,
    scanLogId,
    currentScanSymbol,
    scannedSymbolsCount,
    totalSymbolsToScan,
    scanProgressPercent,
    scanStartedAt,
    scanCompletedAt,
    scanErrorMessage,
    symbolUpdates,
    scanDuration,

    // Existing methods
    fetchRecommendations,
    fetchActiveRecommendations,
    fetchBySymbol,
    fetchStats,
    clearError,

    // SignalR methods
    startSignalR,
    stopSignalR,
    clearScanProgress
  }
})
