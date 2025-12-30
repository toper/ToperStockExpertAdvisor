<script setup lang="ts">
import { computed, ref } from 'vue'
import type { PutRecommendation } from '@/types'

const props = defineProps<{
  recommendation: PutRecommendation
}>()

const contracts = ref(1)
const contractMultiplier = 100 // 1 contract = 100 shares

// Calculations
const maxProfit = computed(() => {
  return (props.recommendation.premium ?? 0) * contracts.value * contractMultiplier
})

const maxLoss = computed(() => {
  // Max loss = (Strike - Premium) * contracts * 100
  // If assigned, you buy shares at strike price
  return ((props.recommendation.strikePrice ?? 0) - (props.recommendation.premium ?? 0)) * contracts.value * contractMultiplier
})

const marginRequired = computed(() => {
  // Rough margin requirement: 20% of strike price * contracts * 100
  return (props.recommendation.strikePrice ?? 0) * 0.20 * contracts.value * contractMultiplier
})

const returnOnMargin = computed(() => {
  if (marginRequired.value === 0) return 0
  return (maxProfit.value / marginRequired.value) * 100
})

const annualizedReturn = computed(() => {
  const days = props.recommendation.daysToExpiry ?? 0
  if (days === 0) return 0
  return (returnOnMargin.value * 365) / days
})

const breakeven = computed(() => {
  return (props.recommendation.strikePrice ?? 0) - (props.recommendation.premium ?? 0)
})

const otmPercent = computed(() => {
  const currentPrice = props.recommendation.currentPrice ?? 0
  if (currentPrice === 0) return 0
  return ((currentPrice - (props.recommendation.strikePrice ?? 0)) / currentPrice) * 100
})
</script>

<template>
  <div class="bg-white rounded-lg shadow p-6">
    <h3 class="text-lg font-semibold text-gray-900 mb-4">
      Profit Calculator - {{ recommendation.symbol }}
    </h3>

    <!-- Contract Input -->
    <div class="mb-6">
      <label class="block text-sm font-medium text-gray-700 mb-2">
        Number of Contracts
      </label>
      <input
        v-model.number="contracts"
        type="number"
        min="1"
        max="100"
        class="w-32 px-3 py-2 border border-gray-300 rounded-md focus:ring-blue-500 focus:border-blue-500"
      />
    </div>

    <!-- Option Details -->
    <div class="grid grid-cols-2 gap-4 mb-6 text-sm">
      <div>
        <span class="text-gray-500">Strike Price:</span>
        <span class="ml-2 font-medium">${{ (recommendation.strikePrice ?? 0).toFixed(2) }}</span>
      </div>
      <div>
        <span class="text-gray-500">Premium:</span>
        <span class="ml-2 font-medium">${{ (recommendation.premium ?? 0).toFixed(2) }}</span>
      </div>
      <div>
        <span class="text-gray-500">Current Price:</span>
        <span class="ml-2 font-medium">${{ (recommendation.currentPrice ?? 0).toFixed(2) }}</span>
      </div>
      <div>
        <span class="text-gray-500">Days to Expiry:</span>
        <span class="ml-2 font-medium">{{ recommendation.daysToExpiry ?? 0 }}</span>
      </div>
    </div>

    <!-- Profit/Loss Analysis -->
    <div class="space-y-4">
      <div class="flex justify-between items-center p-3 bg-green-50 rounded-lg">
        <span class="text-green-700 font-medium">Max Profit (Premium Received)</span>
        <span class="text-green-700 font-bold text-lg">${{ maxProfit.toFixed(2) }}</span>
      </div>

      <div class="flex justify-between items-center p-3 bg-red-50 rounded-lg">
        <span class="text-red-700 font-medium">Max Loss (If Assigned at $0)</span>
        <span class="text-red-700 font-bold text-lg">${{ maxLoss.toFixed(2) }}</span>
      </div>

      <div class="flex justify-between items-center p-3 bg-gray-50 rounded-lg">
        <span class="text-gray-700 font-medium">Breakeven Price</span>
        <span class="text-gray-700 font-bold text-lg">${{ breakeven.toFixed(2) }}</span>
      </div>

      <div class="flex justify-between items-center p-3 bg-blue-50 rounded-lg">
        <span class="text-blue-700 font-medium">OTM Distance</span>
        <span class="text-blue-700 font-bold text-lg">{{ otmPercent.toFixed(1) }}%</span>
      </div>
    </div>

    <!-- Margin & Returns -->
    <div class="mt-6 pt-4 border-t border-gray-200">
      <h4 class="text-sm font-semibold text-gray-700 mb-3">Margin & Returns</h4>
      <div class="grid grid-cols-2 gap-4 text-sm">
        <div>
          <span class="text-gray-500">Est. Margin Required:</span>
          <span class="ml-2 font-medium">${{ marginRequired.toFixed(2) }}</span>
        </div>
        <div>
          <span class="text-gray-500">Return on Margin:</span>
          <span class="ml-2 font-medium text-green-600">{{ returnOnMargin.toFixed(1) }}%</span>
        </div>
        <div class="col-span-2">
          <span class="text-gray-500">Annualized Return:</span>
          <span class="ml-2 font-bold text-green-600">{{ annualizedReturn.toFixed(1) }}%</span>
        </div>
      </div>
    </div>

    <!-- Risk Warning -->
    <div class="mt-4 p-3 bg-yellow-50 rounded-lg text-sm text-yellow-800">
      <strong>Risk Warning:</strong> Selling naked PUTs carries significant risk.
      If the stock drops below the breakeven price, you may be assigned shares at the strike price.
    </div>
  </div>
</template>
