# Outbox, Saga, and Unit of Work Combined Primer
## Mastering Distributed Transaction Patterns

### Executive Summary

Building reliable distributed systems requires careful coordination of state changes across multiple services. This primer explores three fundamental patterns - Outbox, Saga, and Unit of Work - and demonstrates how to combine them effectively to ensure data consistency, reliability, and eventual consistency in microservices architectures.

## Table of Contents

1. [Pattern Fundamentals](#pattern-fundamentals)
2. [The Outbox Pattern](#the-outbox-pattern)
3. [The Saga Pattern](#the-saga-pattern)
4. [The Unit of Work Pattern](#the-unit-of-work-pattern)
5. [Combining the Patterns](#combining-the-patterns)
6. [Implementation Examples](#implementation-examples)
7. [Error Handling and Compensation](#error-handling-and-compensation)
8. [Testing Strategies](#testing-strategies)
9. [Production Considerations](#production-considerations)
10. [Best Practices](#best-practices)

## Pattern Fundamentals

### The Challenge of Distributed Transactions

```
Traditional Monolith:                 Microservices Challenge:
┌─────────────────────┐              ┌──────────┐ ┌──────────┐ ┌──────────┐
│   Database          │              │ Order    │ │ Payment  │ │Inventory │
│  ┌────────────┐     │              │ Service  │ │ Service  │ │ Service  │
│  │ACID Trans  │     │              └─────┬────┘ └────┬─────┘ └────┬─────┘
│  │┌──────────┐│     │                    │           │            │
│  ││All or    ││     │              ┌─────▼────┐ ┌────▼─────┐ ┌────▼─────┐
│  ││Nothing   ││     │              │ Order DB │ │Payment DB│ │Inventory │
│  │└──────────┘│     │              └──────────┘ └──────────┘ │   DB     │
│  └────────────┘     │                                         └──────────┘
└─────────────────────┘              
     Simple & Reliable                    Complex Coordination Required
```

### Pattern Overview

```yaml
Outbox Pattern:
  Purpose: Reliable message publishing
  Guarantees: At-least-once delivery
  Use Case: Publishing events after database changes

Saga Pattern:
  Purpose: Distributed transaction coordination
  Types: Choreography and Orchestration
  Use Case: Multi-step business processes

Unit of Work:
  Purpose: Track and coordinate changes
  Scope: Single transaction boundary
  Use Case: Batch operations and change tracking
```

## The Outbox Pattern

### Core Concept

The Outbox pattern ensures reliable event publishing by storing events in the same database transaction as the business data.

```csharp
public class OutboxPattern
{
    // Database schema
    /*
    CREATE TABLE Outbox (
        Id UNIQUEIDENTIFIER PRIMARY KEY,
        AggregateId NVARCHAR(255),
        EventType NVARCHAR(255),
        EventData NVARCHAR(MAX),
        CreatedAt DATETIME2,
        ProcessedAt DATETIME2 NULL,
        INDEX IX_Outbox_ProcessedAt (ProcessedAt) WHERE ProcessedAt IS NULL
    );
    */
}
```

### Basic Implementation

```csharp
public interface IOutboxService
{
    Task AddEventAsync(OutboxEvent outboxEvent);
    Task<IEnumerable<OutboxEvent>> GetUnprocessedEventsAsync(int batchSize = 100);
    Task MarkAsProcessedAsync(Guid eventId);
}

public class OutboxService : IOutboxService
{
    private readonly ApplicationDbContext _context;
    
    public async Task AddEventAsync(OutboxEvent outboxEvent)
    {
        _context.OutboxEvents.Add(outboxEvent);
        // Note: Don't save here - let Unit of Work handle it
    }
    
    public async Task<IEnumerable<OutboxEvent>> GetUnprocessedEventsAsync(int batchSize = 100)
    {
        return await _context.OutboxEvents
            .Where(e => e.ProcessedAt == null)
            .OrderBy(e => e.CreatedAt)
            .Take(batchSize)
            .ToListAsync();
    }
    
    public async Task MarkAsProcessedAsync(Guid eventId)
    {
        var outboxEvent = await _context.OutboxEvents.FindAsync(eventId);
        if (outboxEvent != null)
        {
            outboxEvent.ProcessedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
}

public class OutboxEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string AggregateId { get; set; }
    public string EventType { get; set; }
    public string EventData { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
}
```

### Outbox Publisher Background Service

```csharp
public class OutboxPublisher : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<OutboxPublisher> _logger;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishOutboxEventsAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing outbox events");
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }
    
    private async Task PublishOutboxEventsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var outboxService = scope.ServiceProvider.GetRequiredService<IOutboxService>();
        
        var events = await outboxService.GetUnprocessedEventsAsync();
        
        foreach (var outboxEvent in events)
        {
            try
            {
                // Deserialize and publish event
                var eventType = Type.GetType(outboxEvent.EventType);
                var domainEvent = JsonSerializer.Deserialize(outboxEvent.EventData, eventType);
                
                await _messageBus.PublishAsync(domainEvent, cancellationToken);
                await outboxService.MarkAsProcessedAsync(outboxEvent.Id);
                
                _logger.LogInformation($"Published event {outboxEvent.Id} of type {outboxEvent.EventType}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to publish event {outboxEvent.Id}");
                // Event will be retried in next iteration
            }
        }
    }
}
```

## The Saga Pattern

### Orchestration-Based Saga

```csharp
public abstract class SagaOrchestrator<TState> where TState : SagaState, new()
{
    protected TState State { get; private set; }
    private readonly List<ISagaStep<TState>> _steps = new();
    private readonly ILogger _logger;
    
    public async Task<SagaResult> ExecuteAsync()
    {
        State = new TState { SagaId = Guid.NewGuid() };
        var executedSteps = new Stack<ISagaStep<TState>>();
        
        try
        {
            foreach (var step in _steps)
            {
                _logger.LogInformation($"Executing step {step.Name} for saga {State.SagaId}");
                
                var result = await step.ExecuteAsync(State);
                
                if (result.IsSuccess)
                {
                    executedSteps.Push(step);
                    State.CompletedSteps.Add(step.Name);
                }
                else
                {
                    _logger.LogError($"Step {step.Name} failed: {result.Error}");
                    await CompensateAsync(executedSteps);
                    return SagaResult.Failed(result.Error);
                }
            }
            
            State.Status = SagaStatus.Completed;
            return SagaResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Saga {State.SagaId} failed with exception");
            await CompensateAsync(executedSteps);
            return SagaResult.Failed(ex.Message);
        }
    }
    
    private async Task CompensateAsync(Stack<ISagaStep<TState>> executedSteps)
    {
        State.Status = SagaStatus.Compensating;
        
        while (executedSteps.Count > 0)
        {
            var step = executedSteps.Pop();
            try
            {
                _logger.LogInformation($"Compensating step {step.Name} for saga {State.SagaId}");
                await step.CompensateAsync(State);
                State.CompensatedSteps.Add(step.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to compensate step {step.Name}");
                // Continue with other compensations
            }
        }
        
        State.Status = SagaStatus.Compensated;
    }
    
    protected void AddStep(ISagaStep<TState> step)
    {
        _steps.Add(step);
    }
}

public interface ISagaStep<TState> where TState : SagaState
{
    string Name { get; }
    Task<StepResult> ExecuteAsync(TState state);
    Task CompensateAsync(TState state);
}

public class SagaState
{
    public Guid SagaId { get; set; }
    public SagaStatus Status { get; set; }
    public List<string> CompletedSteps { get; set; } = new();
    public List<string> CompensatedSteps { get; set; } = new();
    public Dictionary<string, object> Data { get; set; } = new();
}

public enum SagaStatus
{
    Started,
    InProgress,
    Compensating,
    Completed,
    Compensated,
    Failed
}
```

### Order Processing Saga Example

```csharp
public class OrderProcessingSaga : SagaOrchestrator<OrderSagaState>
{
    public OrderProcessingSaga(
        IOrderService orderService,
        IPaymentService paymentService,
        IInventoryService inventoryService,
        IShippingService shippingService,
        ILogger<OrderProcessingSaga> logger) : base(logger)
    {
        AddStep(new CreateOrderStep(orderService));
        AddStep(new ReserveInventoryStep(inventoryService));
        AddStep(new ProcessPaymentStep(paymentService));
        AddStep(new CreateShipmentStep(shippingService));
        AddStep(new UpdateOrderStatusStep(orderService));
    }
}

public class OrderSagaState : SagaState
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public List<OrderItem> Items { get; set; }
    public decimal TotalAmount { get; set; }
    public string PaymentId { get; set; }
    public string ShipmentId { get; set; }
}

public class ProcessPaymentStep : ISagaStep<OrderSagaState>
{
    private readonly IPaymentService _paymentService;
    
    public string Name => "ProcessPayment";
    
    public async Task<StepResult> ExecuteAsync(OrderSagaState state)
    {
        try
        {
            var paymentResult = await _paymentService.ProcessPaymentAsync(new PaymentRequest
            {
                OrderId = state.OrderId,
                CustomerId = state.CustomerId,
                Amount = state.TotalAmount,
                IdempotencyKey = $"{state.SagaId}-payment"
            });
            
            state.PaymentId = paymentResult.PaymentId;
            state.Data["PaymentReference"] = paymentResult.Reference;
            
            return StepResult.Success();
        }
        catch (InsufficientFundsException ex)
        {
            return StepResult.Failure($"Payment failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            return StepResult.Failure($"Unexpected error: {ex.Message}");
        }
    }
    
    public async Task CompensateAsync(OrderSagaState state)
    {
        if (!string.IsNullOrEmpty(state.PaymentId))
        {
            await _paymentService.RefundPaymentAsync(state.PaymentId, "Saga compensation");
        }
    }
}
```

### Choreography-Based Saga

```csharp
public class ChoreographySagaCoordinator
{
    private readonly IEventBus _eventBus;
    private readonly ISagaStateRepository _stateRepository;
    
    public async Task StartSagaAsync<TCommand>(TCommand command) where TCommand : ISagaCommand
    {
        var sagaState = new ChoreographySagaState
        {
            SagaId = Guid.NewGuid(),
            SagaType = typeof(TCommand).Name,
            CurrentStep = "Started",
            StartedAt = DateTime.UtcNow
        };
        
        await _stateRepository.SaveAsync(sagaState);
        
        // Publish initial event to start the saga
        await _eventBus.PublishAsync(new SagaStartedEvent
        {
            SagaId = sagaState.SagaId,
            Command = command
        });
    }
}

// Each service handles its part independently
public class PaymentService : IEventHandler<OrderCreatedEvent>
{
    private readonly IEventBus _eventBus;
    private readonly IOutboxService _outboxService;
    private readonly IUnitOfWork _unitOfWork;
    
    public async Task HandleAsync(OrderCreatedEvent @event)
    {
        using var transaction = await _unitOfWork.BeginTransactionAsync();
        
        try
        {
            // Process payment
            var payment = await ProcessPaymentAsync(@event.OrderId, @event.Amount);
            
            // Add success event to outbox
            await _outboxService.AddEventAsync(new OutboxEvent
            {
                AggregateId = @event.OrderId.ToString(),
                EventType = typeof(PaymentProcessedEvent).FullName,
                EventData = JsonSerializer.Serialize(new PaymentProcessedEvent
                {
                    SagaId = @event.SagaId,
                    OrderId = @event.OrderId,
                    PaymentId = payment.Id,
                    Amount = payment.Amount
                })
            });
            
            await _unitOfWork.CommitAsync();
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync();
            
            // Add failure event to outbox
            await _outboxService.AddEventAsync(new OutboxEvent
            {
                AggregateId = @event.OrderId.ToString(),
                EventType = typeof(PaymentFailedEvent).FullName,
                EventData = JsonSerializer.Serialize(new PaymentFailedEvent
                {
                    SagaId = @event.SagaId,
                    OrderId = @event.OrderId,
                    Reason = ex.Message
                })
            });
            
            await _unitOfWork.CommitAsync();
        }
    }
}
```

## The Unit of Work Pattern

### Core Implementation

```csharp
public interface IUnitOfWork : IDisposable
{
    Task<IDbContextTransaction> BeginTransactionAsync();
    Task CommitAsync();
    Task RollbackAsync();
    Task<int> SaveChangesAsync();
    
    IRepository<T> Repository<T>() where T : class;
    void TrackChange(IChangeTracker change);
    IEnumerable<IChangeTracker> GetPendingChanges();
}

public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;
    private readonly Dictionary<Type, object> _repositories = new();
    private readonly List<IChangeTracker> _changeTrackers = new();
    private IDbContextTransaction _currentTransaction;
    
    public UnitOfWork(ApplicationDbContext context)
    {
        _context = context;
    }
    
    public async Task<IDbContextTransaction> BeginTransactionAsync()
    {
        _currentTransaction = await _context.Database.BeginTransactionAsync();
        return _currentTransaction;
    }
    
    public async Task CommitAsync()
    {
        try
        {
            // Apply all tracked changes
            foreach (var change in _changeTrackers)
            {
                await change.ApplyAsync();
            }
            
            await _context.SaveChangesAsync();
            
            if (_currentTransaction != null)
            {
                await _currentTransaction.CommitAsync();
            }
            
            _changeTrackers.Clear();
        }
        catch
        {
            await RollbackAsync();
            throw;
        }
    }
    
    public async Task RollbackAsync()
    {
        if (_currentTransaction != null)
        {
            await _currentTransaction.RollbackAsync();
        }
        
        // Revert tracked changes
        foreach (var change in _changeTrackers)
        {
            await change.RevertAsync();
        }
        
        _changeTrackers.Clear();
    }
    
    public IRepository<T> Repository<T>() where T : class
    {
        var type = typeof(T);
        if (!_repositories.ContainsKey(type))
        {
            var repositoryType = typeof(Repository<>).MakeGenericType(type);
            _repositories[type] = Activator.CreateInstance(repositoryType, _context);
        }
        
        return (IRepository<T>)_repositories[type];
    }
    
    public void TrackChange(IChangeTracker change)
    {
        _changeTrackers.Add(change);
    }
    
    public IEnumerable<IChangeTracker> GetPendingChanges()
    {
        return _changeTrackers.AsReadOnly();
    }
    
    public void Dispose()
    {
        _currentTransaction?.Dispose();
        _context?.Dispose();
    }
}
```

### Change Tracking

```csharp
public interface IChangeTracker
{
    Guid ChangeId { get; }
    string EntityType { get; }
    string EntityId { get; }
    ChangeType ChangeType { get; }
    DateTime Timestamp { get; }
    Task ApplyAsync();
    Task RevertAsync();
}

public enum ChangeType
{
    Create,
    Update,
    Delete
}

public class EntityChangeTracker<T> : IChangeTracker where T : class
{
    private readonly IRepository<T> _repository;
    private readonly T _entity;
    private readonly T _originalEntity;
    
    public Guid ChangeId { get; } = Guid.NewGuid();
    public string EntityType => typeof(T).Name;
    public string EntityId { get; }
    public ChangeType ChangeType { get; }
    public DateTime Timestamp { get; } = DateTime.UtcNow;
    
    public EntityChangeTracker(
        IRepository<T> repository,
        T entity,
        ChangeType changeType,
        string entityId)
    {
        _repository = repository;
        _entity = entity;
        _originalEntity = changeType == ChangeType.Update ? DeepClone(entity) : null;
        ChangeType = changeType;
        EntityId = entityId;
    }
    
    public async Task ApplyAsync()
    {
        switch (ChangeType)
        {
            case ChangeType.Create:
                await _repository.AddAsync(_entity);
                break;
            case ChangeType.Update:
                await _repository.UpdateAsync(_entity);
                break;
            case ChangeType.Delete:
                await _repository.DeleteAsync(_entity);
                break;
        }
    }
    
    public async Task RevertAsync()
    {
        switch (ChangeType)
        {
            case ChangeType.Create:
                await _repository.DeleteAsync(_entity);
                break;
            case ChangeType.Update:
                await _repository.UpdateAsync(_originalEntity);
                break;
            case ChangeType.Delete:
                await _repository.AddAsync(_entity);
                break;
        }
    }
    
    private T DeepClone(T entity)
    {
        var json = JsonSerializer.Serialize(entity);
        return JsonSerializer.Deserialize<T>(json);
    }
}
```

## Combining the Patterns

### Integrated Implementation

```csharp
public class TransactionalSagaOrchestrator<TState> : SagaOrchestrator<TState> 
    where TState : SagaState, new()
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOutboxService _outboxService;
    private readonly ISagaStateRepository _sagaRepository;
    
    protected override async Task<SagaResult> ExecuteSagaStepAsync(
        ISagaStep<TState> step,
        TState state)
    {
        using var transaction = await _unitOfWork.BeginTransactionAsync();
        
        try
        {
            // Execute the step
            var result = await step.ExecuteAsync(state);
            
            if (result.IsSuccess)
            {
                // Save saga state
                state.LastModified = DateTime.UtcNow;
                state.CurrentStep = step.Name;
                await _sagaRepository.UpdateAsync(state);
                
                // Add events to outbox
                foreach (var @event in result.Events)
                {
                    await _outboxService.AddEventAsync(new OutboxEvent
                    {
                        AggregateId = state.SagaId.ToString(),
                        EventType = @event.GetType().FullName,
                        EventData = JsonSerializer.Serialize(@event)
                    });
                }
                
                // Track all changes
                _unitOfWork.TrackChange(new SagaStepChangeTracker(step, state, true));
                
                // Commit everything atomically
                await _unitOfWork.CommitAsync();
                
                return result;
            }
            else
            {
                await _unitOfWork.RollbackAsync();
                return result;
            }
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackAsync();
            return StepResult.Failure(ex.Message);
        }
    }
}
```

### Complete Order Processing Example

```csharp
public class OrderProcessingService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOutboxService _outboxService;
    private readonly OrderProcessingSaga _saga;
    private readonly ILogger<OrderProcessingService> _logger;
    
    public async Task<OrderResult> ProcessOrderAsync(CreateOrderCommand command)
    {
        using var transaction = await _unitOfWork.BeginTransactionAsync();
        
        try
        {
            // Create order in database
            var order = new Order
            {
                Id = Guid.NewGuid(),
                CustomerId = command.CustomerId,
                Items = command.Items.Select(i => new OrderItem
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity,
                    Price = i.Price
                }).ToList(),
                Status = OrderStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
            
            await _unitOfWork.Repository<Order>().AddAsync(order);
            
            // Track the change
            _unitOfWork.TrackChange(new EntityChangeTracker<Order>(
                _unitOfWork.Repository<Order>(),
                order,
                ChangeType.Create,
                order.Id.ToString()
            ));
            
            // Add order created event to outbox
            await _outboxService.AddEventAsync(new OutboxEvent
            {
                AggregateId = order.Id.ToString(),
                EventType = typeof(OrderCreatedEvent).FullName,
                EventData = JsonSerializer.Serialize(new OrderCreatedEvent
                {
                    OrderId = order.Id,
                    CustomerId = order.CustomerId,
                    Items = order.Items,
                    TotalAmount = order.TotalAmount
                })
            });
            
            // Start saga for complex processing
            var sagaState = new OrderSagaState
            {
                OrderId = order.Id,
                CustomerId = order.CustomerId,
                Items = command.Items,
                TotalAmount = order.TotalAmount
            };
            
            // Add saga started event to outbox
            await _outboxService.AddEventAsync(new OutboxEvent
            {
                AggregateId = sagaState.SagaId.ToString(),
                EventType = typeof(OrderSagaStartedEvent).FullName,
                EventData = JsonSerializer.Serialize(new OrderSagaStartedEvent
                {
                    SagaId = sagaState.SagaId,
                    OrderId = order.Id
                })
            });
            
            // Commit everything atomically
            await _unitOfWork.CommitAsync();
            
            // Execute saga asynchronously (will be handled by saga processor)
            return new OrderResult
            {
                OrderId = order.Id,
                Status = "Processing",
                Message = "Order created and processing started"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process order");
            await _unitOfWork.RollbackAsync();
            throw;
        }
    }
}
```

### Saga State Persistence

```csharp
public interface ISagaStateRepository
{
    Task<T> GetAsync<T>(Guid sagaId) where T : SagaState;
    Task SaveAsync<T>(T state) where T : SagaState;
    Task UpdateAsync<T>(T state) where T : SagaState;
    Task<IEnumerable<T>> GetByStatusAsync<T>(SagaStatus status) where T : SagaState;
}

public class SagaStateRepository : ISagaStateRepository
{
    private readonly IMongoDatabase _database;
    
    public async Task<T> GetAsync<T>(Guid sagaId) where T : SagaState
    {
        var collection = GetCollection<T>();
        var filter = Builders<T>.Filter.Eq(s => s.SagaId, sagaId);
        return await collection.Find(filter).FirstOrDefaultAsync();
    }
    
    public async Task SaveAsync<T>(T state) where T : SagaState
    {
        var collection = GetCollection<T>();
        await collection.InsertOneAsync(state);
    }
    
    public async Task UpdateAsync<T>(T state) where T : SagaState
    {
        var collection = GetCollection<T>();
        var filter = Builders<T>.Filter.Eq(s => s.SagaId, state.SagaId);
        await collection.ReplaceOneAsync(filter, state);
    }
    
    public async Task<IEnumerable<T>> GetByStatusAsync<T>(SagaStatus status) where T : SagaState
    {
        var collection = GetCollection<T>();
        var filter = Builders<T>.Filter.Eq(s => s.Status, status);
        return await collection.Find(filter).ToListAsync();
    }
    
    private IMongoCollection<T> GetCollection<T>() where T : SagaState
    {
        return _database.GetCollection<T>($"saga_{typeof(T).Name}");
    }
}
```

## Error Handling and Compensation

### Compensation Strategies

```csharp
public interface ICompensationStrategy
{
    Task<CompensationResult> CompensateAsync(CompensationContext context);
}

public class RetryWithBackoffCompensation : ICompensationStrategy
{
    private readonly int _maxRetries;
    private readonly TimeSpan _initialDelay;
    
    public async Task<CompensationResult> CompensateAsync(CompensationContext context)
    {
        var retries = 0;
        var delay = _initialDelay;
        
        while (retries < _maxRetries)
        {
            try
            {
                await context.CompensationAction();
                return CompensationResult.Success();
            }
            catch (Exception ex)
            {
                retries++;
                if (retries >= _maxRetries)
                {
                    return CompensationResult.Failed(ex.Message);
                }
                
                await Task.Delay(delay);
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2);
            }
        }
        
        return CompensationResult.Failed("Max retries exceeded");
    }
}

public class CircuitBreakerCompensation : ICompensationStrategy
{
    private readonly ICircuitBreaker _circuitBreaker;
    
    public async Task<CompensationResult> CompensateAsync(CompensationContext context)
    {
        if (!_circuitBreaker.IsOpen)
        {
            try
            {
                await _circuitBreaker.ExecuteAsync(context.CompensationAction);
                return CompensationResult.Success();
            }
            catch (CircuitBreakerOpenException)
            {
                return CompensationResult.Deferred("Circuit breaker is open");
            }
        }
        
        return CompensationResult.Deferred("Waiting for circuit breaker to close");
    }
}
```

### Dead Letter Queue for Failed Sagas

```csharp
public class SagaDeadLetterQueue
{
    private readonly IDeadLetterRepository _repository;
    private readonly INotificationService _notifications;
    
    public async Task SendToDeadLetterAsync<TState>(
        SagaOrchestrator<TState> saga,
        TState state,
        Exception exception) where TState : SagaState
    {
        var deadLetter = new SagaDeadLetter
        {
            Id = Guid.NewGuid(),
            SagaId = state.SagaId,
            SagaType = saga.GetType().Name,
            State = JsonSerializer.Serialize(state),
            Exception = exception.ToString(),
            FailedAt = DateTime.UtcNow,
            RetryCount = state.RetryCount,
            LastStep = state.CurrentStep
        };
        
        await _repository.SaveAsync(deadLetter);
        
        // Notify operations team
        await _notifications.SendAlertAsync(new SagaFailureAlert
        {
            SagaId = state.SagaId,
            SagaType = saga.GetType().Name,
            Error = exception.Message,
            RequiresManualIntervention = true
        });
    }
    
    public async Task<bool> RetryFromDeadLetterAsync(Guid deadLetterId)
    {
        var deadLetter = await _repository.GetAsync(deadLetterId);
        if (deadLetter == null) return false;
        
        try
        {
            // Deserialize state and retry saga
            var stateType = Type.GetType(deadLetter.StateType);
            var state = JsonSerializer.Deserialize(deadLetter.State, stateType) as SagaState;
            
            // Mark as retrying
            deadLetter.RetryingAt = DateTime.UtcNow;
            await _repository.UpdateAsync(deadLetter);
            
            // Re-queue saga for processing
            // Implementation depends on your saga processor
            
            return true;
        }
        catch (Exception ex)
        {
            deadLetter.LastRetryError = ex.Message;
            await _repository.UpdateAsync(deadLetter);
            return false;
        }
    }
}
```

## Testing Strategies

### Unit Testing Sagas

```csharp
[TestFixture]
public class OrderProcessingSagaTests
{
    private Mock<IOrderService> _orderServiceMock;
    private Mock<IPaymentService> _paymentServiceMock;
    private Mock<IInventoryService> _inventoryServiceMock;
    private OrderProcessingSaga _saga;
    
    [SetUp]
    public void Setup()
    {
        _orderServiceMock = new Mock<IOrderService>();
        _paymentServiceMock = new Mock<IPaymentService>();
        _inventoryServiceMock = new Mock<IInventoryService>();
        
        _saga = new OrderProcessingSaga(
            _orderServiceMock.Object,
            _paymentServiceMock.Object,
            _inventoryServiceMock.Object,
            Mock.Of<IShippingService>(),
            Mock.Of<ILogger<OrderProcessingSaga>>()
        );
    }
    
    [Test]
    public async Task ExecuteAsync_AllStepsSucceed_ReturnsSuccess()
    {
        // Arrange
        var state = new OrderSagaState
        {
            OrderId = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            TotalAmount = 100m
        };
        
        _orderServiceMock
            .Setup(x => x.CreateOrderAsync(It.IsAny<Order>()))
            .ReturnsAsync(new OrderResult { Success = true });
            
        _paymentServiceMock
            .Setup(x => x.ProcessPaymentAsync(It.IsAny<PaymentRequest>()))
            .ReturnsAsync(new PaymentResult { Success = true, PaymentId = "PAY123" });
            
        _inventoryServiceMock
            .Setup(x => x.ReserveInventoryAsync(It.IsAny<List<InventoryItem>>()))
            .ReturnsAsync(new InventoryResult { Success = true });
        
        // Act
        var result = await _saga.ExecuteAsync();
        
        // Assert
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(SagaStatus.Completed, _saga.State.Status);
        _orderServiceMock.Verify(x => x.CreateOrderAsync(It.IsAny<Order>()), Times.Once);
        _paymentServiceMock.Verify(x => x.ProcessPaymentAsync(It.IsAny<PaymentRequest>()), Times.Once);
    }
    
    [Test]
    public async Task ExecuteAsync_PaymentFails_CompensatesPreviousSteps()
    {
        // Arrange
        _orderServiceMock
            .Setup(x => x.CreateOrderAsync(It.IsAny<Order>()))
            .ReturnsAsync(new OrderResult { Success = true });
            
        _inventoryServiceMock
            .Setup(x => x.ReserveInventoryAsync(It.IsAny<List<InventoryItem>>()))
            .ReturnsAsync(new InventoryResult { Success = true });
            
        _paymentServiceMock
            .Setup(x => x.ProcessPaymentAsync(It.IsAny<PaymentRequest>()))
            .ThrowsAsync(new InsufficientFundsException());
        
        // Act
        var result = await _saga.ExecuteAsync();
        
        // Assert
        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(SagaStatus.Compensated, _saga.State.Status);
        
        // Verify compensations were called
        _inventoryServiceMock.Verify(x => x.ReleaseInventoryAsync(It.IsAny<List<InventoryItem>>()), Times.Once);
        _orderServiceMock.Verify(x => x.CancelOrderAsync(It.IsAny<Guid>()), Times.Once);
    }
}
```

### Integration Testing with Test Containers

```csharp
[TestFixture]
public class OutboxPatternIntegrationTests
{
    private IServiceProvider _serviceProvider;
    private TestcontainersContainer _sqlContainer;
    private TestcontainersContainer _rabbitMqContainer;
    
    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        // Start SQL Server container
        _sqlContainer = new TestcontainersBuilder<MsSqlTestcontainer>()
            .WithDatabase(new MsSqlTestcontainerConfiguration
            {
                Password = "Strong_password_123!"
            })
            .Build();
            
        await _sqlContainer.StartAsync();
        
        // Start RabbitMQ container
        _rabbitMqContainer = new TestcontainersBuilder<TestcontainersContainer>()
            .WithImage("rabbitmq:3-management")
            .WithPortBinding(5672, 5672)
            .WithPortBinding(15672, 15672)
            .Build();
            
        await _rabbitMqContainer.StartAsync();
        
        // Configure services
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
        
        // Run migrations
        await MigrateDatabase();
    }
    
    [Test]
    public async Task OutboxPublisher_PublishesEventsReliably()
    {
        // Arrange
        var unitOfWork = _serviceProvider.GetRequiredService<IUnitOfWork>();
        var outboxService = _serviceProvider.GetRequiredService<IOutboxService>();
        var messageBus = _serviceProvider.GetRequiredService<IMessageBus>();
        var receivedEvents = new List<OrderCreatedEvent>();
        
        // Subscribe to events
        await messageBus.SubscribeAsync<OrderCreatedEvent>(e =>
        {
            receivedEvents.Add(e);
            return Task.CompletedTask;
        });
        
        // Act - Create order with outbox event
        using (var transaction = await unitOfWork.BeginTransactionAsync())
        {
            var order = new Order { Id = Guid.NewGuid(), CustomerId = Guid.NewGuid() };
            await unitOfWork.Repository<Order>().AddAsync(order);
            
            await outboxService.AddEventAsync(new OutboxEvent
            {
                AggregateId = order.Id.ToString(),
                EventType = typeof(OrderCreatedEvent).FullName,
                EventData = JsonSerializer.Serialize(new OrderCreatedEvent { OrderId = order.Id })
            });
            
            await unitOfWork.CommitAsync();
        }
        
        // Start outbox publisher
        var publisher = _serviceProvider.GetRequiredService<IHostedService>() as OutboxPublisher;
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await publisher.StartAsync(cts.Token);
        
        // Wait for event to be published
        await Task.Delay(TimeSpan.FromSeconds(2));
        
        // Assert
        Assert.AreEqual(1, receivedEvents.Count);
        Assert.IsNotNull(receivedEvents[0].OrderId);
        
        // Verify event marked as processed
        var unprocessedEvents = await outboxService.GetUnprocessedEventsAsync();
        Assert.IsEmpty(unprocessedEvents);
    }
}
```

## Production Considerations

### Monitoring and Observability

```csharp
public class SagaMetrics
{
    private readonly IMetrics _metrics;
    
    public void RecordSagaStarted(string sagaType)
    {
        _metrics.Measure.Counter.Increment("saga_started", new MetricTags("type", sagaType));
    }
    
    public void RecordSagaCompleted(string sagaType, TimeSpan duration)
    {
        _metrics.Measure.Counter.Increment("saga_completed", new MetricTags("type", sagaType));
        _metrics.Measure.Histogram.Update("saga_duration", duration.TotalMilliseconds, new MetricTags("type", sagaType));
    }
    
    public void RecordSagaFailed(string sagaType, string failureReason)
    {
        _metrics.Measure.Counter.Increment("saga_failed", 
            new MetricTags("type", sagaType, "reason", failureReason));
    }
    
    public void RecordStepExecuted(string sagaType, string stepName, bool success, TimeSpan duration)
    {
        _metrics.Measure.Counter.Increment("saga_step_executed",
            new MetricTags("saga", sagaType, "step", stepName, "success", success.ToString()));
        _metrics.Measure.Histogram.Update("saga_step_duration", 
            duration.TotalMilliseconds,
            new MetricTags("saga", sagaType, "step", stepName));
    }
}

public class OutboxMetrics
{
    private readonly IMetrics _metrics;
    
    public void RecordEventPublished(string eventType, TimeSpan publishDelay)
    {
        _metrics.Measure.Counter.Increment("outbox_event_published", 
            new MetricTags("type", eventType));
        _metrics.Measure.Histogram.Update("outbox_publish_delay", 
            publishDelay.TotalMilliseconds,
            new MetricTags("type", eventType));
    }
    
    public void RecordPublishFailure(string eventType, string error)
    {
        _metrics.Measure.Counter.Increment("outbox_publish_failed",
            new MetricTags("type", eventType, "error", error));
    }
    
    public void SetPendingEvents(int count)
    {
        _metrics.Measure.Gauge.SetValue("outbox_pending_events", count);
    }
}
```

### Performance Optimization

```csharp
public class OptimizedOutboxPublisher : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IMessageBus _messageBus;
    private readonly Channel<OutboxEvent> _eventChannel;
    private readonly SemaphoreSlim _publishSemaphore;
    
    public OptimizedOutboxPublisher(IServiceProvider serviceProvider, IMessageBus messageBus)
    {
        _serviceProvider = serviceProvider;
        _messageBus = messageBus;
        _eventChannel = Channel.CreateUnbounded<OutboxEvent>();
        _publishSemaphore = new SemaphoreSlim(10); // Limit concurrent publishes
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollTask = PollOutboxAsync(stoppingToken);
        var publishTask = PublishEventsAsync(stoppingToken);
        
        await Task.WhenAll(pollTask, publishTask);
    }
    
    private async Task PollOutboxAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var outboxService = scope.ServiceProvider.GetRequiredService<IOutboxService>();
                
                // Batch fetch events
                var events = await outboxService.GetUnprocessedEventsAsync(batchSize: 1000);
                
                // Queue for publishing
                foreach (var @event in events)
                {
                    await _eventChannel.Writer.WriteAsync(@event, cancellationToken);
                }
                
                // Adaptive polling - increase delay if no events found
                var delay = events.Any() ? TimeSpan.FromSeconds(1) : TimeSpan.FromSeconds(10);
                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception ex)
            {
                // Log and continue
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            }
        }
    }
    
    private async Task PublishEventsAsync(CancellationToken cancellationToken)
    {
        await foreach (var @event in _eventChannel.Reader.ReadAllAsync(cancellationToken))
        {
            await _publishSemaphore.WaitAsync(cancellationToken);
            
            _ = Task.Run(async () =>
            {
                try
                {
                    await PublishEventAsync(@event);
                }
                finally
                {
                    _publishSemaphore.Release();
                }
            }, cancellationToken);
        }
    }
}
```

### Configuration and Deployment

```yaml
# docker-compose.yml
version: '3.8'

services:
  api:
    build: .
    environment:
      - ConnectionStrings__Default=Server=db;Database=SagaDemo;User=sa;Password=Pass@word1
      - RabbitMQ__Host=rabbitmq
      - Outbox__Enabled=true
      - Outbox__PollingInterval=5
      - Saga__TimeoutMinutes=30
    depends_on:
      - db
      - rabbitmq
      
  outbox-processor:
    build: .
    command: ["dotnet", "OutboxProcessor.dll"]
    environment:
      - ConnectionStrings__Default=Server=db;Database=SagaDemo;User=sa;Password=Pass@word1
      - RabbitMQ__Host=rabbitmq
    depends_on:
      - db
      - rabbitmq
      
  saga-processor:
    build: .
    command: ["dotnet", "SagaProcessor.dll"]
    environment:
      - ConnectionStrings__Default=Server=db;Database=SagaDemo;User=sa;Password=Pass@word1
      - MongoDB__ConnectionString=mongodb://mongo:27017
      - Saga__MaxConcurrency=10
    depends_on:
      - db
      - mongo
      
  db:
    image: mcr.microsoft.com/mssql/server:2019-latest
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=Pass@word1
    volumes:
      - db-data:/var/opt/mssql
      
  mongo:
    image: mongo:5
    volumes:
      - mongo-data:/data/db
      
  rabbitmq:
    image: rabbitmq:3-management
    ports:
      - "15672:15672"
    volumes:
      - rabbitmq-data:/var/lib/rabbitmq

volumes:
  db-data:
  mongo-data:
  rabbitmq-data:
```

## Best Practices

### Pattern Selection Guidelines

```yaml
Use Outbox Pattern When:
  - You need guaranteed event delivery
  - Events must be published after database changes
  - You can tolerate eventual consistency
  - You have reliable message infrastructure

Use Saga Pattern When:
  - Business process spans multiple services
  - You need distributed transaction coordination
  - Compensation logic is well-defined
  - Long-running processes are acceptable

Use Unit of Work When:
  - Multiple entities change together
  - You need change tracking
  - Batch operations are common
  - Transaction boundaries are clear

Combine All Three When:
  - Building event-driven microservices
  - Consistency and reliability are critical
  - Complex business processes exist
  - Audit trail is required
```

### Implementation Checklist

```markdown
## Outbox Implementation
- [ ] Outbox table with proper indexes
- [ ] Background service for publishing
- [ ] Idempotent event publishing
- [ ] Monitoring and metrics
- [ ] Dead letter handling
- [ ] Retention policy

## Saga Implementation
- [ ] State persistence strategy
- [ ] Compensation for each step
- [ ] Timeout handling
- [ ] Retry policies
- [ ] Monitoring and alerting
- [ ] Manual intervention process

## Unit of Work Implementation
- [ ] Transaction scope management
- [ ] Change tracking
- [ ] Repository pattern
- [ ] Rollback capability
- [ ] Performance optimization
- [ ] Connection management
```

### Common Pitfalls and Solutions

```csharp
// Pitfall: Not handling duplicate events
public class IdempotentEventHandler<TEvent> : IEventHandler<TEvent>
{
    private readonly IEventHandler<TEvent> _innerHandler;
    private readonly IIdempotencyStore _idempotencyStore;
    
    public async Task HandleAsync(TEvent @event, EventContext context)
    {
        var idempotencyKey = $"{context.MessageId}-{typeof(TEvent).Name}";
        
        if (await _idempotencyStore.ExistsAsync(idempotencyKey))
        {
            // Event already processed
            return;
        }
        
        await _innerHandler.HandleAsync(@event, context);
        await _idempotencyStore.MarkProcessedAsync(idempotencyKey, TimeSpan.FromDays(7));
    }
}

// Pitfall: Not handling saga timeouts
public class TimeoutAwareSaga<TState> : SagaOrchestrator<TState> where TState : SagaState, new()
{
    private readonly TimeSpan _timeout;
    
    protected override async Task<SagaResult> ExecuteAsync()
    {
        using var cts = new CancellationTokenSource(_timeout);
        
        try
        {
            return await ExecuteWithCancellationAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            await CompensateAsync();
            return SagaResult.Failed("Saga timeout exceeded");
        }
    }
}
```

## Conclusion

The combination of Outbox, Saga, and Unit of Work patterns provides a robust foundation for building reliable distributed systems:

### Key Benefits
1. **Reliability**: Guaranteed message delivery and transaction consistency
2. **Resilience**: Built-in compensation and error handling
3. **Observability**: Clear audit trail and monitoring points
4. **Flexibility**: Supports both choreography and orchestration
5. **Testability**: Clear boundaries and mockable components

### Architecture Principles
- **Eventual Consistency**: Embrace it, don't fight it
- **Idempotency**: Design all operations to be idempotent
- **Compensation**: Plan for failure from the start
- **Monitoring**: Instrument everything
- **Simplicity**: Start simple, add complexity as needed

The patterns work best when combined thoughtfully, with each pattern handling its specific concern while integrating seamlessly with the others to create a cohesive, reliable system.