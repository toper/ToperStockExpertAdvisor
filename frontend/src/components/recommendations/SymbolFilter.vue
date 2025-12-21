<script setup lang="ts">
import { FunnelIcon } from '@heroicons/vue/24/outline'

defineProps<{
  symbols: string[]
  selectedSymbol: string | null
}>()

const emit = defineEmits<{
  select: [symbol: string | null]
}>()
</script>

<template>
  <div class="flex items-center gap-2">
    <FunnelIcon class="h-5 w-5 text-gray-400" />
    <div class="flex flex-wrap gap-2">
      <button
        @click="emit('select', null)"
        :class="[
          'px-3 py-1.5 rounded-full text-sm font-medium transition-colors',
          selectedSymbol === null
            ? 'bg-primary-600 text-white'
            : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
        ]"
      >
        All
      </button>
      <button
        v-for="symbol in symbols"
        :key="symbol"
        @click="emit('select', symbol)"
        :class="[
          'px-3 py-1.5 rounded-full text-sm font-medium transition-colors',
          selectedSymbol === symbol
            ? 'bg-primary-600 text-white'
            : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
        ]"
      >
        {{ symbol }}
      </button>
    </div>
  </div>
</template>
