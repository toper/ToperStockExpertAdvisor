<script setup lang="ts">
import { ref } from 'vue'
import type { PutRecommendation } from '@/types'
import RecommendationCard from './RecommendationCard.vue'
import Modal from '@/components/common/Modal.vue'
import ProfitCalculator from '@/components/charts/ProfitCalculator.vue'
import ScoreDetailsModal from './ScoreDetailsModal.vue'

defineProps<{
  recommendations: PutRecommendation[]
}>()

const showCalculator = ref(false)
const showScoreDetails = ref(false)
const selectedRecommendation = ref<PutRecommendation | null>(null)

function openCalculator(recommendation: PutRecommendation) {
  selectedRecommendation.value = recommendation
  showCalculator.value = true
}

function closeCalculator() {
  showCalculator.value = false
  selectedRecommendation.value = null
}

function openScoreDetails(recommendation: PutRecommendation) {
  selectedRecommendation.value = recommendation
  showScoreDetails.value = true
}

function closeScoreDetails() {
  showScoreDetails.value = false
  selectedRecommendation.value = null
}
</script>

<template>
  <div>
    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
      <RecommendationCard
        v-for="rec in recommendations"
        :key="rec.id"
        :recommendation="rec"
        @calculate="openCalculator"
        @view-details="openScoreDetails"
      />
      <div
        v-if="!recommendations.length"
        class="col-span-full text-center py-12 text-gray-500"
      >
        No recommendations found
      </div>
    </div>

    <!-- Calculator Modal -->
    <Modal
      :open="showCalculator"
      :title="`Profit Calculator - ${selectedRecommendation?.symbol || ''}`"
      @close="closeCalculator"
    >
      <ProfitCalculator
        v-if="selectedRecommendation"
        :recommendation="selectedRecommendation"
      />
    </Modal>

    <!-- Score Details Modal -->
    <ScoreDetailsModal
      :open="showScoreDetails"
      :recommendation="selectedRecommendation"
      @close="closeScoreDetails"
    />
  </div>
</template>
