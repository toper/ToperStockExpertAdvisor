<script setup lang="ts">
import { computed } from 'vue'
import type { PutRecommendation } from '@/types'
import Modal from '@/components/common/Modal.vue'

const props = defineProps<{
  open: boolean
  recommendation: PutRecommendation | null
}>()

const emit = defineEmits<{
  close: []
}>()

const fScoreCategory = computed(() => {
  const score = props.recommendation?.piotroskiFScore
  if (score === undefined || score === null) return null

  if (score >= 7) return { label: 'Doskonała kondycja', class: 'bg-green-50 border-green-200 text-green-800' }
  if (score >= 4) return { label: 'Średnia kondycja', class: 'bg-yellow-50 border-yellow-200 text-yellow-800' }
  return { label: 'Słaba kondycja', class: 'bg-red-50 border-red-200 text-red-800' }
})

const zScoreCategory = computed(() => {
  const score = props.recommendation?.altmanZScore
  if (score === undefined || score === null) return null

  if (score > 2.99) return { label: 'Strefa bezpieczna', class: 'bg-green-50 border-green-200 text-green-800' }
  if (score >= 1.81) return { label: 'Strefa szara - niepewność', class: 'bg-yellow-50 border-yellow-200 text-yellow-800' }
  return { label: 'Strefa zagrożenia', class: 'bg-red-50 border-red-200 text-red-800' }
})
</script>

<template>
  <Modal
    :open="open"
    :title="`Analiza wskaźników - ${recommendation?.symbol || ''}`"
    @close="emit('close')"
  >
    <div v-if="recommendation" class="space-y-6">
      <!-- F-Score (Piotroski) Section -->
      <div class="bg-gray-50 rounded-lg p-5">
        <div class="flex items-start justify-between mb-4">
          <div>
            <h4 class="text-lg font-semibold text-gray-900">Piotroski F-Score</h4>
            <p class="text-sm text-gray-600 mt-1">Wskaźnik kondycji finansowej spółki (0-9)</p>
          </div>
          <div class="text-right">
            <div class="text-3xl font-bold text-gray-900">
              {{ recommendation.piotroskiFScore ?? '-' }}
            </div>
            <div v-if="fScoreCategory" :class="['inline-block px-3 py-1 rounded-full text-sm font-medium border mt-2', fScoreCategory.class]">
              {{ fScoreCategory.label }}
            </div>
          </div>
        </div>

        <div class="space-y-2">
          <div class="flex items-center justify-between py-2 px-3 bg-white rounded border border-gray-200">
            <span class="text-sm text-gray-700">7-9 punktów</span>
            <span class="text-sm font-medium text-green-700">Doskonała kondycja</span>
          </div>
          <div class="flex items-center justify-between py-2 px-3 bg-white rounded border border-gray-200">
            <span class="text-sm text-gray-700">4-6 punktów</span>
            <span class="text-sm font-medium text-yellow-700">Średnia kondycja</span>
          </div>
          <div class="flex items-center justify-between py-2 px-3 bg-white rounded border border-gray-200">
            <span class="text-sm text-gray-700">0-3 punkty</span>
            <span class="text-sm font-medium text-red-700">Słaba kondycja</span>
          </div>
        </div>

        <div class="mt-4 p-3 bg-blue-50 border border-blue-200 rounded">
          <p class="text-xs text-blue-900">
            <strong>Info:</strong> F-Score Piotroski'ego ocenia kondycję finansową spółki w 9 obszarach:
            rentowność, efektywność operacyjna i struktura kapitału. Wyższy wynik oznacza lepszą kondycję finansową.
          </p>
        </div>
      </div>

      <!-- Z-Score (Altman) Section -->
      <div class="bg-gray-50 rounded-lg p-5">
        <div class="flex items-start justify-between mb-4">
          <div>
            <h4 class="text-lg font-semibold text-gray-900">Altman Z-Score</h4>
            <p class="text-sm text-gray-600 mt-1">Wskaźnik ryzyka bankructwa</p>
          </div>
          <div class="text-right">
            <div class="text-3xl font-bold text-gray-900">
              {{ recommendation.altmanZScore?.toFixed(2) ?? '-' }}
            </div>
            <div v-if="zScoreCategory" :class="['inline-block px-3 py-1 rounded-full text-sm font-medium border mt-2', zScoreCategory.class]">
              {{ zScoreCategory.label }}
            </div>
          </div>
        </div>

        <div class="space-y-2">
          <div class="flex items-center justify-between py-2 px-3 bg-white rounded border border-gray-200">
            <span class="text-sm text-gray-700">&gt; 2.99</span>
            <span class="text-sm font-medium text-green-700">Strefa bezpieczna</span>
          </div>
          <div class="flex items-center justify-between py-2 px-3 bg-white rounded border border-gray-200">
            <span class="text-sm text-gray-700">1.81 - 2.99</span>
            <span class="text-sm font-medium text-yellow-700">Strefa szara (niepewność)</span>
          </div>
          <div class="flex items-center justify-between py-2 px-3 bg-white rounded border border-gray-200">
            <span class="text-sm text-gray-700">&lt; 1.81</span>
            <span class="text-sm font-medium text-red-700">Strefa zagrożenia</span>
          </div>
        </div>

        <div class="mt-4 p-3 bg-blue-50 border border-blue-200 rounded">
          <p class="text-xs text-blue-900">
            <strong>Info:</strong> Z-Score Altmana przewiduje prawdopodobieństwo bankructwa spółki w ciągu 2 lat.
            Uwzględnia płynność finansową, rentowność, dźwignię finansową i kapitał obrotowy. Wyższy wynik oznacza niższe ryzyko.
          </p>
        </div>
      </div>
    </div>
  </Modal>
</template>
