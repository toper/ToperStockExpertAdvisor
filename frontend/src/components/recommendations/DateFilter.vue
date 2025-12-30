<script setup lang="ts">
import { ref, computed } from 'vue'
import { format, subDays, startOfDay, endOfDay } from 'date-fns'
import { CalendarIcon, XMarkIcon } from '@heroicons/vue/24/outline'

const emit = defineEmits<{
  'update:dateRange': [{ from: Date | null; to: Date | null }]
}>()

const dateFrom = ref<string>('')
const dateTo = ref<string>('')

const today = format(new Date(), 'yyyy-MM-dd')

const quickFilters = [
  { label: 'Today', days: 0 },
  { label: 'Last 3 days', days: 3 },
  { label: 'Last 7 days', days: 7 },
  { label: 'Last 30 days', days: 30 }
]

function applyQuickFilter(days: number) {
  const to = new Date()
  const from = days === 0 ? startOfDay(to) : subDays(to, days)

  dateFrom.value = format(from, 'yyyy-MM-dd')
  dateTo.value = format(to, 'yyyy-MM-dd')

  emitDateRange()
}

function emitDateRange() {
  const from = dateFrom.value ? startOfDay(new Date(dateFrom.value)) : null
  const to = dateTo.value ? endOfDay(new Date(dateTo.value)) : null

  emit('update:dateRange', { from, to })
}

function clearFilters() {
  dateFrom.value = ''
  dateTo.value = ''
  emit('update:dateRange', { from: null, to: null })
}

const hasActiveFilter = computed(() => {
  return dateFrom.value !== '' || dateTo.value !== ''
})
</script>

<template>
  <div class="bg-white rounded-xl shadow-sm border border-gray-200 p-4">
    <div class="flex items-center justify-between mb-4">
      <div class="flex items-center gap-2">
        <CalendarIcon class="h-5 w-5 text-gray-500" />
        <h3 class="text-sm font-semibold text-gray-700">Scan Date Filter</h3>
      </div>

      <button
        v-if="hasActiveFilter"
        @click="clearFilters"
        class="text-sm text-gray-500 hover:text-gray-700 flex items-center gap-1"
      >
        <XMarkIcon class="h-4 w-4" />
        Clear
      </button>
    </div>

    <!-- Quick Filters -->
    <div class="flex flex-wrap gap-2 mb-4">
      <button
        v-for="filter in quickFilters"
        :key="filter.label"
        @click="applyQuickFilter(filter.days)"
        class="px-3 py-1.5 text-xs font-medium text-gray-700 bg-gray-100 rounded-lg hover:bg-gray-200 transition-colors"
      >
        {{ filter.label }}
      </button>
    </div>

    <!-- Custom Date Range -->
    <div class="grid grid-cols-2 gap-3">
      <div>
        <label for="date-from" class="block text-xs font-medium text-gray-600 mb-1">
          From
        </label>
        <input
          id="date-from"
          v-model="dateFrom"
          type="date"
          :max="dateTo || today"
          @change="emitDateRange"
          class="w-full px-3 py-2 text-sm border border-gray-300 rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-primary-500"
        />
      </div>

      <div>
        <label for="date-to" class="block text-xs font-medium text-gray-600 mb-1">
          To
        </label>
        <input
          id="date-to"
          v-model="dateTo"
          type="date"
          :min="dateFrom"
          :max="today"
          @change="emitDateRange"
          class="w-full px-3 py-2 text-sm border border-gray-300 rounded-lg focus:ring-2 focus:ring-primary-500 focus:border-primary-500"
        />
      </div>
    </div>
  </div>
</template>
