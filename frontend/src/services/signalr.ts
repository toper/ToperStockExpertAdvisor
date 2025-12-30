import * as signalR from '@microsoft/signalr'

export interface ScanProgressUpdate {
  symbol: string
  currentIndex: number
  totalSymbols: number
  status: 'Scanning' | 'Completed' | 'Error'
  timestamp: string
  errorMessage?: string
  recommendationsCount: number
  progressPercent: number
  metrics?: {
    piotroskiFScore?: number
    altmanZScore?: number
  }
}

export interface ScanStartedEvent {
  scanLogId: number
  totalSymbols: number
  timestamp: string
}

export interface ScanCompletedEvent {
  id: number
  startedAt: string
  completedAt?: string
  symbolsScanned: number
  recommendationsGenerated: number
  status: string
  errorMessage?: string
  duration: number
}

export type ScanEventHandlers = {
  onScanStarted?: (event: ScanStartedEvent) => void
  onSymbolScanning?: (update: ScanProgressUpdate) => void
  onSymbolCompleted?: (update: ScanProgressUpdate) => void
  onSymbolError?: (update: ScanProgressUpdate) => void
  onScanCompleted?: (event: ScanCompletedEvent) => void
  onConnected?: () => void
  onDisconnected?: (error?: Error) => void
  onReconnecting?: () => void
  onReconnected?: () => void
}

/**
 * SignalR Hub Connection Manager
 * Manages real-time connection to scan progress updates
 */
export class ScanProgressHubClient {
  private connection: signalR.HubConnection | null = null
  private handlers: ScanEventHandlers = {}
  private hubUrl: string

  constructor(hubUrl: string) {
    this.hubUrl = hubUrl
  }

  /**
   * Start the SignalR connection
   */
  async start(handlers: ScanEventHandlers = {}): Promise<void> {
    this.handlers = handlers

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(this.hubUrl, {
        skipNegotiation: false,
        transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.ServerSentEvents
      })
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: (retryContext) => {
          // Exponential backoff: 0s, 2s, 10s, 30s, then 60s
          if (retryContext.previousRetryCount === 0) return 0
          if (retryContext.previousRetryCount === 1) return 2000
          if (retryContext.previousRetryCount === 2) return 10000
          if (retryContext.previousRetryCount === 3) return 30000
          return 60000
        }
      })
      .configureLogging(signalR.LogLevel.Information)
      .build()

    // Setup event handlers
    this.setupEventHandlers()

    // Setup connection lifecycle handlers
    this.connection.onclose((error) => {
      console.warn('SignalR connection closed', error)
      this.handlers.onDisconnected?.(error)
    })

    this.connection.onreconnecting(() => {
      console.log('SignalR reconnecting...')
      this.handlers.onReconnecting?.()
    })

    this.connection.onreconnected(() => {
      console.log('SignalR reconnected')
      this.handlers.onReconnected?.()
      // Rejoin the group after reconnection
      this.joinScanListeners()
    })

    try {
      await this.connection.start()
      console.log('SignalR connected to', this.hubUrl)
      this.handlers.onConnected?.()

      // Join the scan listeners group
      await this.joinScanListeners()
    } catch (err) {
      console.error('Error starting SignalR connection:', err)
      this.handlers.onDisconnected?.(err as Error)
      throw err
    }
  }

  /**
   * Setup event handlers for hub methods
   */
  private setupEventHandlers(): void {
    if (!this.connection) return

    this.connection.on('ScanStarted', (event: ScanStartedEvent) => {
      console.log('Scan started:', event)
      this.handlers.onScanStarted?.(event)
    })

    this.connection.on('SymbolScanning', (update: ScanProgressUpdate) => {
      console.log('Symbol scanning:', update)
      this.handlers.onSymbolScanning?.(update)
    })

    this.connection.on('SymbolCompleted', (update: ScanProgressUpdate) => {
      console.log('Symbol completed:', update)
      this.handlers.onSymbolCompleted?.(update)
    })

    this.connection.on('SymbolError', (update: ScanProgressUpdate) => {
      console.warn('Symbol error:', update)
      this.handlers.onSymbolError?.(update)
    })

    this.connection.on('ScanCompleted', (event: ScanCompletedEvent) => {
      console.log('Scan completed:', event)
      this.handlers.onScanCompleted?.(event)
    })
  }

  /**
   * Join the scan listeners group to receive updates
   */
  private async joinScanListeners(): Promise<void> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      return
    }

    try {
      await this.connection.invoke('JoinScanListeners')
      console.log('Joined scan-listeners group')
    } catch (err) {
      console.error('Error joining scan listeners group:', err)
    }
  }

  /**
   * Leave the scan listeners group
   */
  async leaveScanListeners(): Promise<void> {
    if (!this.connection || this.connection.state !== signalR.HubConnectionState.Connected) {
      return
    }

    try {
      await this.connection.invoke('LeaveScanListeners')
      console.log('Left scan-listeners group')
    } catch (err) {
      console.error('Error leaving scan listeners group:', err)
    }
  }

  /**
   * Stop the SignalR connection
   */
  async stop(): Promise<void> {
    if (!this.connection) return

    try {
      await this.leaveScanListeners()
      await this.connection.stop()
      console.log('SignalR connection stopped')
    } catch (err) {
      console.error('Error stopping SignalR connection:', err)
    }
  }

  /**
   * Get current connection state
   */
  get state(): signalR.HubConnectionState | null {
    return this.connection?.state ?? null
  }

  /**
   * Check if connected
   */
  get isConnected(): boolean {
    return this.connection?.state === signalR.HubConnectionState.Connected
  }
}

/**
 * Create a new SignalR hub client instance
 */
export function createScanProgressHub(baseUrl: string = 'http://localhost:5001'): ScanProgressHubClient {
  const hubUrl = `${baseUrl}/hubs/scan-progress`
  return new ScanProgressHubClient(hubUrl)
}
