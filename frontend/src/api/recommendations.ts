import apiClient from './client'
import type { RecommendationsResult, RecommendationsGridResult } from '@/types'

export interface GetRecommendationsParams {
  minDays?: number
  maxDays?: number
}

export interface RecommendationsStats {
  totalRecords: number
  healthyStocksCount: number
  withOptionsCount: number
  minFScore: number
}

export async function getRecommendations(params?: GetRecommendationsParams): Promise<RecommendationsResult> {
  const response = await apiClient.get<RecommendationsResult>('/recommendations', { params })
  return response.data
}

export async function getRecommendationsBySymbol(symbol: string): Promise<RecommendationsResult> {
  const response = await apiClient.get<RecommendationsResult>(`/recommendations/${symbol}`)
  return response.data
}

export async function getActiveRecommendations(): Promise<RecommendationsGridResult> {
  const response = await apiClient.get<RecommendationsGridResult>('/recommendations/active')
  return response.data
}

export async function getRecommendationsStats(): Promise<RecommendationsStats> {
  const response = await apiClient.get<RecommendationsStats>('/recommendations/stats')
  return response.data
}
