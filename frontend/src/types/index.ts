export interface PutRecommendation {
  id: number
  symbol: string
  currentPrice: number
  strikePrice: number
  expiry: string
  daysToExpiry: number
  premium: number
  breakeven: number
  confidence: number
  expectedGrowthPercent: number
  strategyName: string
  scannedAt: string
  potentialReturn: number
  otmPercent: number
  piotroskiFScore?: number
  altmanZScore?: number
}

export interface Result<T> {
  data: T | null
  isValid: boolean
  errors: string[]
}

export interface GridResult<T> extends Result<T> {
  totalCount: number
}

export type RecommendationsResult = Result<PutRecommendation[]>
export type RecommendationsGridResult = GridResult<PutRecommendation[]>
