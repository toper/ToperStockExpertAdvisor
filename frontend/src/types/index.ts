export interface StockData {
  id: number
  symbol: string
  modificationTime: string // Last update time - replaces scannedAt

  // SimFin metrics
  piotroskiFScore?: number
  altmanZScore?: number
  roa?: number
  debtToEquity?: number
  currentRatio?: number
  marketCapBillions?: number

  // Options data
  currentPrice?: number
  strikePrice?: number
  expiry?: string
  daysToExpiry?: number
  premium?: number
  breakeven?: number
  confidence?: number
  expectedGrowthPercent?: number
  strategyName?: string
  exanteSymbol?: string
  optionPrice?: number
  volume?: number
  openInterest?: number

  // Calculated fields
  potentialReturn: number
  otmPercent: number
}

// Legacy alias for backward compatibility
export type PutRecommendation = StockData

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
