<script setup lang="ts">
import { XCircleIcon, XMarkIcon } from '@heroicons/vue/24/outline'

defineProps<{
  message: string
}>()

const emit = defineEmits<{
  close: []
}>()
</script>

<template>
  <div class="rounded-lg bg-red-50 p-4 border border-red-200">
    <div class="flex">
      <div class="flex-shrink-0">
        <XCircleIcon class="h-5 w-5 text-red-400" />
      </div>
      <div class="ml-3 flex-1">
        <p class="text-sm text-red-700">{{ message }}</p>
      </div>
      <div class="ml-auto pl-3">
        <button
          @click="emit('close')"
          class="inline-flex rounded-md p-1.5 text-red-500 hover:bg-red-100 focus:outline-none focus:ring-2 focus:ring-red-600 focus:ring-offset-2 focus:ring-offset-red-50"
        >
          <XMarkIcon class="h-5 w-5" />
        </button>
      </div>
    </div>
  </div>
</template>
