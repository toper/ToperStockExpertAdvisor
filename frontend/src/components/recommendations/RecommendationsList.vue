<script setup lang="ts">
import type { PutRecommendation } from '@/types'
import RecommendationCard from './RecommendationCard.vue'

defineProps<{
  recommendations: PutRecommendation[]
}>()
</script>

<template>
  <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
    <RecommendationCard
      v-for="rec in recommendations"
      :key="rec.id"
      :recommendation="rec"
    />
    <div
      v-if="!recommendations.length"
      class="col-span-full text-center py-12 text-gray-500"
    >
      No recommendations found
    </div>
  </div>
</template>
