<script setup lang="ts">
import { computed } from 'vue'
import { useRecommendationsStore } from '@/stores/recommendations'
import { ArrowPathIcon, CheckCircleIcon, XCircleIcon } from '@heroicons/vue/24/outline'

const store = useRecommendationsStore()

const barClass = computed(() => {
  if (store.scanInProgress) return 'bg-blue-600'
  if (store.scanErrorMessage) return 'bg-red-600'
  return 'bg-green-600'
})

const barIcon = computed(() => {
  if (store.scanInProgress) return ArrowPathIcon
  if (store.scanErrorMessage) return XCircleIcon
  return CheckCircleIcon
})

const primaryMessage = computed(() => {
  if (store.scanInProgress && store.currentScanSymbol) {
    return `Scanning: ${store.currentScanSymbol}`
  }
  if (store.scanInProgress) {
    return 'Starting scan...'
  }
  if (store.scanErrorMessage) {
    return 'Scan failed'
  }
  if (store.scanCompletedAt) {
    return 'Scan completed!'
  }
  return ''
})

const secondaryMessage = computed(() => {
  if (store.scanInProgress) {
    return `Progress: ${store.scannedSymbolsCount}/${store.totalSymbolsToScan} symbols (${store.scanProgressPercent}%)`
  }
  if (store.scanErrorMessage) {
    return store.scanErrorMessage
  }
  if (store.scanCompletedAt) {
    return `Found ${store.recommendations.length} recommendations in ${store.scanDuration.toFixed(0)}s`
  }
  return ''
})
</script>

<template>
  <Transition
    enter-active-class="transition-all duration-300 ease-out"
    enter-from-class="translate-y-full"
    enter-to-class="translate-y-0"
    leave-active-class="transition-all duration-200 ease-in"
    leave-from-class="translate-y-0"
    leave-to-class="translate-y-full"
  >
    <div
      v-if="store.scanInProgress || (store.scanCompletedAt && !store.scanErrorMessage)"
      class="fixed bottom-0 left-0 right-0 z-50 shadow-2xl"
      :class="barClass"
    >
      <div class="container mx-auto px-4 py-3">
        <div class="flex items-center gap-4 text-white">
          <!-- Icon -->
          <component
            :is="barIcon"
            :class="['h-6 w-6 flex-shrink-0', store.scanInProgress && 'animate-spin']"
          />

          <!-- Content -->
          <div class="flex-1 min-w-0">
            <div class="flex items-baseline gap-3">
              <span class="font-semibold text-base">{{ primaryMessage }}</span>
              <span class="text-sm text-white/90">{{ secondaryMessage }}</span>
            </div>

            <!-- Progress bar -->
            <div
              v-if="store.scanInProgress && store.totalSymbolsToScan > 0"
              class="mt-2 bg-white/20 rounded-full h-1 overflow-hidden"
            >
              <div
                class="bg-white h-full transition-all duration-300"
                :style="{ width: `${store.scanProgressPercent}%` }"
              />
            </div>
          </div>

          <!-- Close button (only when not scanning) -->
          <button
            v-if="!store.scanInProgress"
            @click="store.clearScanProgress"
            class="text-white/80 hover:text-white transition-colors p-1 hover:bg-white/10 rounded"
            aria-label="Close notification"
          >
            <XCircleIcon class="h-5 w-5" />
          </button>
        </div>
      </div>
    </div>
  </Transition>
</template>
