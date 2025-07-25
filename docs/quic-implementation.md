            
            await this.transport.ready;
            console.log('QUIC connection established');
            
            // Set up incoming stream handler
            this.handleIncomingStreams();
            
        } catch (error) {
            console.error('Failed to establish QUIC connection:', error);
            throw error;
        }
    }
    
    private async handleIncomingStreams(): Promise<void> {
        if (!this.transport) return;
        
        const reader = this.transport.incomingUnidirectionalStreams.getReader();
        
        while (true) {
            const { done, value } = await reader.read();
            if (done) break;
            
            // Handle incoming stream
            this.processIncomingStream(value);
        }
    }
    
    private async processIncomingStream(stream: ReadableStream): Promise<void> {
        const reader = stream.getReader();
        
        try {
            // Read stream type
            const { value: streamTypeArray } = await reader.read();
            const streamType = new Uint8Array(streamTypeArray)[0];
            
            switch (streamType) {
                case 0x01: // Piece movement
                    await this.handlePieceMovementStream(reader);
                    break;
                case 0x02: // Cursor updates
                    await this.handleCursorUpdateStream(reader);
                    break;
                default:
                    console.warn('Unknown stream type:', streamType);
            }
        } finally {
            reader.releaseLock();
        }
    }
    
    async sendPieceMovement(pieceId: string, x: number, y: number, rotation: number): Promise<void> {
        if (!this.transport) throw new Error('Not connected');
        
        const stream = await this.getOrCreateOutboundStream('piece-movement');
        const writer = stream.getWriter();
        
        try {
            const message = this.encodePieceMovement(pieceId, x, y, rotation);
            await writer.write(message);
        } finally {
            writer.releaseLock();
        }
    }
    
    private encodePieceMovement(pieceId: string, x: number, y: number, rotation: number): Uint8Array {
        const buffer = new ArrayBuffer(37); // 1 + 16 + 4 + 4 + 2 + 8 + 2 padding
        const view = new DataView(buffer);
        
        // Message type
        view.setUint8(0, 0x01);
        
        // Piece ID (16 bytes) - convert UUID string to bytes
        const pieceIdBytes = this.uuidToBytes(pieceId);
        for (let i = 0; i < 16; i++) {
            view.setUint8(1 + i, pieceIdBytes[i]);
        }
        
        // Coordinates and rotation
        view.setInt32(17, x, true); // little endian
        view.setInt32(21, y, true);
        view.setInt16(25, rotation, true);
        
        // Timestamp
        const timestamp = BigInt(Date.now() * 10000); // Convert to .NET ticks
        view.setBigInt64(27, timestamp, true);
        
        return new Uint8Array(buffer);
    }
    
    private async getOrCreateOutboundStream(purpose: string): Promise<WritableStream> {
        if (this.streams.has(purpose)) {
            return this.streams.get(purpose)!;
        }
        
        if (!this.transport) throw new Error('Not connected');
        
        const stream = await this.transport.createUnidirectionalStream();
        this.streams.set(purpose, stream);
        
        return stream;
    }
}
```

### 2. Performance Monitoring
```typescript
class QuicPerformanceMonitor {
    private latencyMeasurements: number[] = [];
    private throughputMeasurements: number[] = [];
    
    measureLatency(startTime: number): void {
        const latency = performance.now() - startTime;
        this.latencyMeasurements.push(latency);
        
        // Keep only last 100 measurements
        if (this.latencyMeasurements.length > 100) {
            this.latencyMeasurements.shift();
        }
    }
    
    getAverageLatency(): number {
        if (this.latencyMeasurements.length === 0) return 0;
        
        const sum = this.latencyMeasurements.reduce((a, b) => a + b, 0);
        return sum / this.latencyMeasurements.length;
    }
    
    getP95Latency(): number {
        if (this.latencyMeasurements.length === 0) return 0;
        
        const sorted = [...this.latencyMeasurements].sort((a, b) => a - b);
        const p95Index = Math.floor(sorted.length * 0.95);
        return sorted[p95Index];
    }
}
```

## Advanced QUIC Features

### 1. 0-RTT Connection Resumption
```csharp
public class QuicSessionCache
{
    private readonly IMemoryCache _cache;
    
    public async Task<byte[]?> GetSessionTicketAsync(string clientId)
    {
        return await _cache.GetAsync($"quic_session_{clientId}") as byte[];
    }
    
    public async Task StoreSessionTicketAsync(string clientId, byte[] sessionTicket)
    {
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24),
            SlidingExpiration = TimeSpan.FromHours(4)
        };
        
        _cache.Set($"quic_session_{clientId}", sessionTicket, options);
    }
}
```

### 2. Adaptive Congestion Control
```csharp
public class QuicCongestionController
{
    private readonly QuicConnectionOptions _options;
    
    public void ConfigureCongestionControl(NetworkCondition condition)
    {
        switch (condition)
        {
            case NetworkCondition.HighLatency:
                _options.MaxBidirectionalStreams = 10;
                _options.MaxUnidirectionalStreams = 20;
                _options.DefaultStreamErrorCode = QuicError.ConnectionTimeout;
                break;
                
            case NetworkCondition.LowBandwidth:
                _options.MaxBidirectionalStreams = 5;
                _options.MaxUnidirectionalStreams = 10;
                _options.DefaultCloseErrorCode = QuicError.ConnectionLimitExceeded;
                break;
                
            case NetworkCondition.Optimal:
                _options.MaxBidirectionalStreams = 50;
                _options.MaxUnidirectionalStreams = 100;
                break;
        }
    }
}
```

### 3. Flow Control Management
```csharp
public class QuicFlowController
{
    private readonly SemaphoreSlim _sendSemaphore;
    private long _currentWindowSize;
    private long _maxWindowSize;
    
    public async Task<bool> CanSendAsync(int messageSize)
    {
        await _sendSemaphore.WaitAsync();
        
        try
        {
            if (_currentWindowSize >= messageSize)
            {
                _currentWindowSize -= messageSize;
                return true;
            }
            
            return false;
        }
        finally
        {
            _sendSemaphore.Release();
        }
    }
    
    public void UpdateWindowSize(long newWindowSize)
    {
        Interlocked.Exchange(ref _currentWindowSize, newWindowSize);
    }
}
```

## Security Implementation

### 1. TLS 1.3 Configuration
```csharp
public class QuicSecurityConfiguration
{
    public void ConfigureTls13(SslServerAuthenticationOptions options)
    {
        options.SslProtocols = SslProtocols.Tls13;
        options.CipherSuitesPolicy = new CipherSuitesPolicy(new[]
        {
            TlsCipherSuite.TLS_AES_256_GCM_SHA384,
            TlsCipherSuite.TLS_AES_128_GCM_SHA256,
            TlsCipherSuite.TLS_CHACHA20_POLY1305_SHA256
        });
        
        // Enable perfect forward secrecy
        options.EncryptionPolicy = EncryptionPolicy.RequireEncryption;
        
        // Configure client certificate validation
        options.ClientCertificateRequired = false; // Use token-based auth instead
        options.CertificateRevocationCheckMode = X509RevocationMode.Online;
    }
}
```

### 2. Connection ID Rotation
```csharp
public class QuicConnectionIdManager
{
    private readonly Timer _rotationTimer;
    private readonly ConcurrentDictionary<byte[], QuicConnection> _activeConnections;
    
    public QuicConnectionIdManager()
    {
        // Rotate connection IDs every 5 minutes for security
        _rotationTimer = new Timer(RotateConnectionIds, null, 
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }
    
    private void RotateConnectionIds(object? state)
    {
        foreach (var (connectionId, connection) in _activeConnections)
        {
            // Generate new connection ID
            var newConnectionId = GenerateSecureConnectionId();
            
            // Update connection mapping
            _activeConnections.TryRemove(connectionId, out _);
            _activeConnections.TryAdd(newConnectionId, connection);
            
            // Notify peer of new connection ID
            connection.RotateConnectionId(newConnectionId);
        }
    }
}
```

## Error Handling and Resilience

### 1. Connection Recovery
```csharp
public class QuicConnectionRecovery
{
    private readonly ILogger<QuicConnectionRecovery> _logger;
    private readonly ExponentialBackoff _backoff;
    
    public async Task<QuicConnection> RecoverConnectionAsync(
        QuicConnectionOptions options, 
        CancellationToken cancellationToken)
    {
        var attempt = 0;
        var maxAttempts = 5;
        
        while (attempt < maxAttempts)
        {
            try
            {
                var connection = await QuicConnection.ConnectAsync(options, cancellationToken);
                _logger.LogInformation("QUIC connection recovered after {Attempts} attempts", attempt + 1);
                return connection;
            }
            catch (Exception ex) when (attempt < maxAttempts - 1)
            {
                attempt++;
                var delay = _backoff.GetDelay(attempt);
                
                _logger.LogWarning(ex, "QUIC connection attempt {Attempt} failed, retrying in {Delay}ms", 
                    attempt, delay.TotalMilliseconds);
                
                await Task.Delay(delay, cancellationToken);
            }
        }
        
        throw new InvalidOperationException($"Failed to establish QUIC connection after {maxAttempts} attempts");
    }
}
```

### 2. Stream Error Handling
```csharp
public class QuicStreamErrorHandler
{
    public async Task HandleStreamErrorAsync(QuicStream stream, Exception error)
    {
        switch (error)
        {
            case QuicException quicEx when quicEx.QuicError == QuicError.StreamLimitExceeded:
                await HandleStreamLimitExceeded(stream);
                break;
                
            case QuicException quicEx when quicEx.QuicError == QuicError.FlowControlViolation:
                await HandleFlowControlViolation(stream);
                break;
                
            case OperationCanceledException:
                // Stream was cancelled, clean up resources
                await CleanupStreamResources(stream);
                break;
                
            default:
                _logger.LogError(error, "Unhandled QUIC stream error");
                await stream.AbortAsync(QuicAbortDirection.Both, 1);
                break;
        }
    }
}
```

## Performance Optimization

### 1. Buffer Pool Management
```csharp
public class QuicBufferPool
{
    private readonly ObjectPool<byte[]> _bufferPool;
    private readonly int _bufferSize;
    
    public QuicBufferPool(int bufferSize = 4096)
    {
        _bufferSize = bufferSize;
        _bufferPool = new DefaultObjectPool<byte[]>(
            new DefaultPooledObjectPolicy<byte[]>(), 
            maximumRetained: 100);
    }
    
    public byte[] RentBuffer()
    {
        var buffer = _bufferPool.Get();
        if (buffer.Length != _bufferSize)
        {
            buffer = new byte[_bufferSize];
        }
        return buffer;
    }
    
    public void ReturnBuffer(byte[] buffer)
    {
        if (buffer.Length == _bufferSize)
        {
            Array.Clear(buffer, 0, buffer.Length);
            _bufferPool.Return(buffer);
        }
    }
}
```

### 2. Message Batching
```csharp
public class QuicMessageBatcher
{
    private readonly List<QueuedMessage> _messageQueue = new();
    private readonly Timer _flushTimer;
    private readonly QuicStream _stream;
    private readonly object _lock = new();
    
    public void QueueMessage(byte[] message, MessagePriority priority)
    {
        lock (_lock)
        {
            _messageQueue.Add(new QueuedMessage(message, priority, DateTimeOffset.UtcNow));
            
            // Flush immediately for high priority messages
            if (priority == MessagePriority.High || _messageQueue.Count >= 10)
            {
                _ = Task.Run(FlushQueueAsync);
            }
        }
    }
    
    private async Task FlushQueueAsync()
    {
        List<QueuedMessage> messagesToSend;
        
        lock (_lock)
        {
            if (_messageQueue.Count == 0) return;
            
            messagesToSend = new List<QueuedMessage>(_messageQueue);
            _messageQueue.Clear();
        }
        
        // Sort by priority and timestamp
        messagesToSend.Sort((a, b) =>
        {
            var priorityComparison = b.Priority.CompareTo(a.Priority);
            return priorityComparison != 0 ? priorityComparison : a.Timestamp.CompareTo(b.Timestamp);
        });
        
        // Send batched messages
        var batchedData = CombineMessages(messagesToSend.Select(m => m.Data));
        await _stream.WriteAsync(batchedData);
        await _stream.FlushAsync();
    }
}
```

## Integration with Existing Infrastructure

### 1. HTTP/3 Fallback Strategy
```csharp
public class ProtocolNegotiator
{
    public async Task<IClientConnection> EstablishConnectionAsync(
        string serverUrl, 
        ConnectionPreferences preferences)
    {
        // Try QUIC/HTTP3 first
        if (preferences.PreferQuic && IsQuicSupported())
        {
            try
            {
                return await EstablishQuicConnectionAsync(serverUrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "QUIC connection failed, falling back to HTTP/2");
            }
        }
        
        // Fallback to HTTP/2 over TLS
        if (preferences.AllowHttp2)
        {
            try
            {
                return await EstablishHttp2ConnectionAsync(serverUrl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "HTTP/2 connection failed, falling back to WebSocket");
            }
        }
        
        // Final fallback to WebSocket
        return await EstablishWebSocketConnectionAsync(serverUrl);
    }
}
```

### 2. Load Balancer Configuration
```yaml
# Azure Application Gateway configuration for QUIC
apiVersion: networking.azure.com/v1
kind: ApplicationGateway
metadata:
  name: puzzle-gateway
spec:
  frontendPorts:
    - name: https-port
      port: 443
      protocol: HTTPS
    - name: quic-port
      port: 443
      protocol: HTTP3  # QUIC over HTTP/3
  
  backendAddressPools:
    - name: puzzle-backend
      addresses:
        - fqdn: puzzle-backend.namespace.svc.cluster.local
  
  listeners:
    - name: quic-listener
      protocol: HTTP3
      port: quic-port
      hostName: puzzle.example.com
```

## Monitoring and Observability

### 1. QUIC Metrics Collection
```csharp
public class QuicMetricsCollector
{
    private readonly IMetrics _metrics;
    
    public void RecordConnectionMetrics(QuicConnection connection)
    {
        _metrics.CreateHistogram<double>("quic_connection_establishment_time")
            .Record(connection.EstablishmentTime.TotalMilliseconds);
        
        _metrics.CreateCounter<long>("quic_connections_total")
            .Add(1, new KeyValuePair<string, object?>("status", "established"));
        
        _metrics.CreateGauge<int>("quic_active_connections")
            .Record(GetActiveConnectionCount());
    }
    
    public void RecordStreamMetrics(QuicStream stream, int messageSize)
    {
        _metrics.CreateHistogram<int>("quic_message_size_bytes")
            .Record(messageSize);
        
        _metrics.CreateCounter<long>("quic_messages_total")
            .Add(1, new KeyValuePair<string, object?>("stream_type", GetStreamType(stream)));
    }
}
```

### 2. Performance Dashboard
```csharp
public class QuicPerformanceDashboard
{
    public class QuicStats
    {
        public double AverageLatency { get; set; }
        public double P95Latency { get; set; }
        public double P99Latency { get; set; }
        public long MessagesPerSecond { get; set; }
        public long BytesPerSecond { get; set; }
        public int ActiveConnections { get; set; }
        public double PacketLossRate { get; set; }
        public int AverageRTT { get; set; }
    }
    
    public async Task<QuicStats> GetCurrentStatsAsync()
    {
        // Collect metrics from all active connections
        // Calculate aggregated statistics
        // Return performance snapshot
    }
}
```

This comprehensive QUIC implementation provides ultra-low latency communication for the most demanding real-time collaboration scenarios in the puzzle platform, while maintaining security and reliability standards.
