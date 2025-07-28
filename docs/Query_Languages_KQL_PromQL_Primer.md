# KQL and PromQL Query Languages Primer
## Mastering Observability Query Languages

### Executive Summary

Modern observability requires powerful query languages to extract insights from massive volumes of logs, metrics, and traces. This primer covers Kusto Query Language (KQL) used in Azure services and Prometheus Query Language (PromQL) for metrics analysis, providing practical patterns for effective monitoring and troubleshooting.

## Table of Contents

1. [KQL Fundamentals](#kql-fundamentals)
2. [KQL Advanced Patterns](#kql-advanced-patterns)
3. [PromQL Fundamentals](#promql-fundamentals)
4. [PromQL Advanced Patterns](#promql-advanced-patterns)
5. [Common Use Cases](#common-use-cases)
6. [Performance Optimization](#performance-optimization)
7. [Integration Patterns](#integration-patterns)
8. [Alerting Strategies](#alerting-strategies)
9. [Visualization Best Practices](#visualization-best-practices)
10. [Query Comparison](#query-comparison)

## KQL Fundamentals

### What is KQL?

Kusto Query Language (KQL) is a powerful query language used across Azure services including:
- Application Insights
- Log Analytics
- Azure Data Explorer
- Microsoft Defender
- Azure Monitor

### Basic KQL Structure

```kql
// Basic query structure
TableName
| where TimeGenerated > ago(1h)
| summarize count() by bin(TimeGenerated, 5m)
| order by TimeGenerated desc
| take 100
```

### Core Operators

```kql
// Filtering
requests
| where success == false
| where duration > 1000
| where customDimensions.Environment == "Production"

// Projection
requests
| project 
    timestamp,
    duration,
    resultCode,
    Environment = tostring(customDimensions.Environment)

// Aggregation
requests
| summarize 
    RequestCount = count(),
    AvgDuration = avg(duration),
    P95Duration = percentile(duration, 95)
    by bin(timestamp, 5m)

// Joining
requests
| join kind=inner (
    exceptions
    | project timestamp, operation_Id, message
) on operation_Id

// Time series
requests
| make-series 
    RequestsPerMin = count() 
    on timestamp 
    from ago(1h) to now() 
    step 1m
```

### String Operations

```kql
// String matching
traces
| where message contains "error"
| where message !contains "warning"
| where message startswith "Failed"
| where message endswith ".jpg"
| where message matches regex @"Error:\s\d+"

// String manipulation
traces
| extend 
    ErrorCode = extract(@"Error:\s(\d+)", 1, message),
    Username = tostring(split(customDimensions.User, "@")[0]),
    Domain = tostring(split(customDimensions.User, "@")[1])

// Parse operators
customEvents
| where name == "ApiCall"
| extend details = parse_json(tostring(customDimensions.Details))
| project 
    timestamp,
    api = tostring(details.api),
    duration = toint(details.duration)
```

### Time Operations

```kql
// Time filtering
requests
| where timestamp > ago(1h)
| where timestamp between (datetime(2024-01-01) .. datetime(2024-01-31))
| where dayofweek(timestamp) in (1, 7) // Monday or Sunday

// Time bucketing
requests
| summarize count() by bin(timestamp, 5m)
| summarize count() by bin(timestamp, 1h)
| summarize count() by startofday(timestamp)

// Time calculations
requests
| extend 
    HourOfDay = hourofday(timestamp),
    DayOfWeek = dayofweek(timestamp),
    WeekOfYear = weekofyear(timestamp)
| where HourOfDay between (9 .. 17) // Business hours
```

## KQL Advanced Patterns

### Performance Analysis

```kql
// Identify slow requests
let threshold = 1000; // milliseconds
requests
| where duration > threshold
| summarize 
    Count = count(),
    AvgDuration = avg(duration),
    P95 = percentile(duration, 95),
    P99 = percentile(duration, 99)
    by name, bin(timestamp, 5m)
| where Count > 10 // Filter noise
| order by P99 desc

// Performance degradation detection
let baseline = 
    requests
    | where timestamp between (ago(7d) .. ago(1d))
    | summarize P95Baseline = percentile(duration, 95) by name;
requests
| where timestamp > ago(1h)
| summarize P95Current = percentile(duration, 95) by name
| join kind=inner baseline on name
| extend DegradationPercent = (P95Current - P95Baseline) / P95Baseline * 100
| where DegradationPercent > 20
| project name, P95Baseline, P95Current, DegradationPercent
```

### Error Analysis

```kql
// Error rate calculation
requests
| summarize 
    TotalRequests = count(),
    FailedRequests = countif(success == false)
    by bin(timestamp, 5m)
| extend ErrorRate = round(100.0 * FailedRequests / TotalRequests, 2)
| where ErrorRate > 1 // Alert threshold

// Error pattern detection
exceptions
| where timestamp > ago(1h)
| extend 
    ErrorType = tostring(split(type, ".")[-1]),
    ErrorMessage = substring(innermostMessage, 0, 100)
| summarize 
    Count = count(),
    UniqueOperations = dcount(operation_Name),
    SampleMessage = any(innermostMessage)
    by ErrorType, ErrorMessage
| order by Count desc
| take 20

// Correlated errors
let errorWindow = 5m;
let errors = 
    exceptions
    | where timestamp > ago(1h)
    | project timestamp, operation_Id, type;
requests
| where timestamp > ago(1h)
| where success == false
| join kind=inner errors on operation_Id
| summarize 
    ErrorTypes = make_set(type),
    ErrorCount = count()
    by bin(timestamp, errorWindow), name
| where ErrorCount > 5
```

### User Behavior Analysis

```kql
// User journey analysis
let sessionTimeout = 30m;
pageViews
| where timestamp > ago(1d)
| sort by user_Id, timestamp asc
| extend 
    SessionId = row_cumsum(
        iff(timestamp - prev(timestamp, 1) > sessionTimeout, 1, 0), 
        user_Id
    )
| summarize 
    PageSequence = make_list(name),
    Duration = max(timestamp) - min(timestamp),
    PageCount = count()
    by user_Id, SessionId
| where PageCount > 3
| take 100

// Funnel analysis
let step1 = pageViews | where name == "HomePage" | distinct user_Id;
let step2 = pageViews | where name == "ProductPage" | distinct user_Id;
let step3 = pageViews | where name == "CheckoutPage" | distinct user_Id;
let step4 = customEvents | where name == "Purchase" | distinct user_Id;
union
    (step1 | extend Step = "1. HomePage" | count),
    (step1 | join kind=inner step2 on user_Id | extend Step = "2. ProductPage" | count),
    (step1 | join kind=inner step2 on user_Id | join kind=inner step3 on user_Id | extend Step = "3. CheckoutPage" | count),
    (step1 | join kind=inner step2 on user_Id | join kind=inner step3 on user_Id | join kind=inner step4 on user_Id | extend Step = "4. Purchase" | count)
| project Step, Users = Count
```

### Advanced Analytics

```kql
// Anomaly detection using built-in functions
requests
| where timestamp > ago(7d)
| make-series 
    RequestRate = count() 
    on timestamp 
    from ago(7d) to now() 
    step 1h
| extend (anomalies, score, baseline) = series_decompose_anomalies(RequestRate, 1.5)
| mv-expand timestamp, RequestRate, anomalies, score, baseline
| where anomalies != 0
| project timestamp, RequestRate, baseline, score

// Forecasting
requests
| where timestamp > ago(30d)
| make-series 
    DailyRequests = count() 
    on timestamp 
    from ago(30d) to now() + 7d 
    step 1d
| extend forecast = series_decompose_forecast(DailyRequests, 7)
| mv-expand timestamp, DailyRequests, forecast
| where timestamp > now()
| project timestamp, ForecastedRequests = forecast

// Correlation analysis
let metric1 = 
    performanceCounters
    | where name == "% Processor Time"
    | summarize AvgCPU = avg(value) by bin(timestamp, 5m);
let metric2 = 
    requests
    | summarize AvgDuration = avg(duration) by bin(timestamp, 5m);
metric1
| join kind=inner metric2 on timestamp
| extend Correlation = series_pearson_correlation(AvgCPU, AvgDuration)
```

## PromQL Fundamentals

### What is PromQL?

Prometheus Query Language (PromQL) is designed for querying time series data in Prometheus and compatible systems like:
- Prometheus
- Cortex
- Thanos
- VictoriaMetrics
- Grafana Mimir

### Basic PromQL Structure

```promql
# Instant vector - current values
http_requests_total

# Range vector - values over time
http_requests_total[5m]

# Scalar - single numerical value
100

# String - text value (limited use)
"production"
```

### Selectors and Matchers

```promql
# Label matchers
http_requests_total{method="GET"}
http_requests_total{method!="GET"}
http_requests_total{method=~"GET|POST"}
http_requests_total{method!~"PUT|DELETE"}

# Multiple labels
http_requests_total{method="GET", status="200", env="prod"}

# All metrics matching pattern
{__name__=~"http_.*"}
```

### Basic Operations

```promql
# Rate calculation (most common)
rate(http_requests_total[5m])

# Increase over time window
increase(http_requests_total[1h])

# Instant rate (less accurate)
irate(http_requests_total[5m])

# Aggregation
sum(rate(http_requests_total[5m]))
avg(rate(http_requests_total[5m]))
max(rate(http_requests_total[5m]))
min(rate(http_requests_total[5m]))
count(rate(http_requests_total[5m]))
```

### Aggregation Operators

```promql
# Group by labels
sum by (method, status) (rate(http_requests_total[5m]))

# Keep all labels except specified
sum without (instance, job) (rate(http_requests_total[5m]))

# Topk/bottomk
topk(5, rate(http_requests_total[5m]))
bottomk(3, http_response_time_seconds)

# Quantiles
quantile(0.95, http_request_duration_seconds)
histogram_quantile(0.95, rate(http_request_duration_bucket[5m]))
```

## PromQL Advanced Patterns

### Performance Monitoring

```promql
# Request rate by endpoint
sum by (endpoint) (rate(http_requests_total[5m]))

# Error rate percentage
100 * sum(rate(http_requests_total{status=~"5.."}[5m])) 
  / sum(rate(http_requests_total[5m]))

# Latency percentiles
histogram_quantile(0.95,
  sum by (endpoint, le) (
    rate(http_request_duration_seconds_bucket[5m])
  )
)

# SLI - Success rate
1 - (
  sum(rate(http_requests_total{status=~"5.."}[5m]))
  / sum(rate(http_requests_total[5m]))
)

# Apdex score
(
  sum(rate(http_request_duration_seconds_bucket{le="0.5"}[5m])) +
  sum(rate(http_request_duration_seconds_bucket{le="2"}[5m])) / 2
) / sum(rate(http_request_duration_seconds_count[5m]))
```

### Resource Utilization

```promql
# CPU usage percentage
100 - (avg by (instance) (irate(node_cpu_seconds_total{mode="idle"}[5m])) * 100)

# Memory usage percentage
100 * (1 - (node_memory_MemAvailable_bytes / node_memory_MemTotal_bytes))

# Disk usage percentage
100 - (node_filesystem_free_bytes{fstype!~"tmpfs|fuse.lxcfs|squashfs"} 
  / node_filesystem_size_bytes * 100)

# Network traffic
rate(node_network_receive_bytes_total[5m]) + 
rate(node_network_transmit_bytes_total[5m])

# Container resource usage
sum by (pod) (rate(container_cpu_usage_seconds_total[5m]))
sum by (pod) (container_memory_usage_bytes)
```

### Kubernetes Monitoring

```promql
# Pod restarts
increase(kube_pod_container_status_restarts_total[1h])

# Deployment replicas mismatch
kube_deployment_spec_replicas - kube_deployment_status_replicas

# Node pressure conditions
kube_node_status_condition{condition=~"DiskPressure|MemoryPressure|PIDPressure",status="true"}

# Pod resource requests vs usage
sum by (pod) (container_memory_usage_bytes) 
  / sum by (pod) (kube_pod_container_resource_requests{resource="memory"})

# Persistent volume usage
kubelet_volume_stats_used_bytes / kubelet_volume_stats_capacity_bytes * 100
```

### Business Metrics

```promql
# Revenue per minute
sum(rate(payment_processed_total[5m])) * avg(payment_amount_dollars)

# Conversion rate
sum(rate(checkout_completed_total[1h])) 
  / sum(rate(checkout_started_total[1h])) * 100

# Active users (using gauge)
sum(increase(user_activity_timestamp[5m] > 0))

# API usage by customer
sum by (customer_id) (rate(api_requests_total[5m]))

# SLA compliance
avg_over_time(
  (sum(rate(http_requests_total{status!~"5.."}[5m])) 
    / sum(rate(http_requests_total[5m])))[30d:1h]
) * 100
```

### Advanced Calculations

```promql
# Rate of change detection
deriv(gauge_metric[5m])

# Prediction (linear extrapolation)
predict_linear(node_filesystem_free_bytes[1h], 4 * 3600)

# Smoothing
avg_over_time(volatile_metric[5m])

# Z-score calculation
(metric - avg_over_time(metric[1h])) 
  / stddev_over_time(metric[1h])

# Day-over-day comparison
rate(http_requests_total[5m]) 
  / rate(http_requests_total[5m] offset 1d)
```

## Common Use Cases

### Application Performance Monitoring

```kql
// KQL - Request performance dashboard
requests
| where timestamp > ago(1h)
| summarize 
    RequestCount = count(),
    AvgDuration = avg(duration),
    P50 = percentile(duration, 50),
    P95 = percentile(duration, 95),
    P99 = percentile(duration, 99),
    ErrorRate = round(100.0 * countif(success == false) / count(), 2)
    by bin(timestamp, 1m), name
| order by timestamp desc
```

```promql
# PromQL - Request performance dashboard
# Request rate
sum by (endpoint) (rate(http_requests_total[5m]))

# Latency percentiles
histogram_quantile(0.95,
  sum by (endpoint, le) (
    rate(http_request_duration_seconds_bucket[5m])
  )
)

# Error rate
sum by (endpoint) (rate(http_requests_total{status=~"5.."}[5m]))
  / sum by (endpoint) (rate(http_requests_total[5m])) * 100
```

### Infrastructure Monitoring

```kql
// KQL - Resource utilization
performanceCounters
| where timestamp > ago(30m)
| where name in ("% Processor Time", "Available MBytes", "Disk Reads/sec", "Disk Writes/sec")
| summarize 
    AvgValue = avg(value),
    MaxValue = max(value)
    by bin(timestamp, 1m), computer, name
| evaluate pivot(name, any(AvgValue))
```

```promql
# PromQL - Resource utilization
# CPU usage by instance
100 - (avg by (instance) (irate(node_cpu_seconds_total{mode="idle"}[5m])) * 100)

# Memory usage by instance
(node_memory_MemTotal_bytes - node_memory_MemAvailable_bytes) 
  / node_memory_MemTotal_bytes * 100

# Disk I/O
rate(node_disk_read_bytes_total[5m]) + rate(node_disk_written_bytes_total[5m])
```

### User Experience Monitoring

```kql
// KQL - Page load performance
pageViews
| where timestamp > ago(1h)
| extend LoadTimeBucket = case(
    duration < 1000, "Fast (<1s)",
    duration < 3000, "Moderate (1-3s)",
    duration < 5000, "Slow (3-5s)",
    "Very Slow (>5s)"
    )
| summarize 
    Count = count(),
    AvgDuration = avg(duration)
    by LoadTimeBucket
| order by AvgDuration asc
```

```promql
# PromQL - User experience metrics
# Page load time distribution
histogram_quantile(0.95,
  sum by (page, le) (
    rate(page_load_duration_seconds_bucket[5m])
  )
)

# Active sessions
sum(increase(user_session_duration_seconds[5m] > 0))

# Feature usage
sum by (feature) (rate(feature_used_total[1h]))
```

## Performance Optimization

### KQL Optimization

```kql
// Use time filters first
requests
| where timestamp > ago(1h)  // Always filter time first
| where success == false
| summarize count() by name

// Avoid expensive operations on large datasets
// Bad
requests
| extend Hour = hourofday(timestamp)
| where Hour between (9 .. 17)

// Good
requests
| where timestamp > ago(1h) and hourofday(timestamp) between (9 .. 17)

// Use projection to reduce data
requests
| where timestamp > ago(1h)
| project timestamp, name, duration, success  // Only needed columns
| summarize avg(duration) by name

// Pre-aggregate when possible
requests
| where timestamp > ago(24h)
| summarize count() by bin(timestamp, 1h), name
| where count_ > 1000  // Filter after aggregation
```

### PromQL Optimization

```promql
# Use recording rules for expensive queries
# prometheus.rules.yml
groups:
  - name: aggregated_metrics
    interval: 30s
    rules:
      - record: instance:node_cpu_utilization:rate5m
        expr: |
          100 - (avg by (instance) (
            irate(node_cpu_seconds_total{mode="idle"}[5m])
          ) * 100)
      
      - record: job:http_requests:rate5m
        expr: |
          sum by (job, method, status) (
            rate(http_requests_total[5m])
          )

# Use efficient label matching
# Bad - regex matching on high cardinality
http_requests_total{instance=~"prod-.*"}

# Good - exact matching
http_requests_total{env="prod"}

# Limit time ranges appropriately
# Don't use unnecessarily long ranges
rate(http_requests_total[5m])  # Good for dashboards
rate(http_requests_total[1h])  # Only if needed for smoothing
```

## Integration Patterns

### KQL with Application Insights

```kql
// Custom metrics correlation
let customMetrics = customMetrics
| where timestamp > ago(1h)
| where name == "OrderProcessingTime"
| summarize AvgProcessingTime = avg(value) by bin(timestamp, 5m);
requests
| where timestamp > ago(1h)
| where name contains "Order"
| summarize AvgDuration = avg(duration) by bin(timestamp, 5m)
| join kind=inner customMetrics on timestamp
| project timestamp, AvgDuration, AvgProcessingTime
| extend Correlation = AvgDuration / AvgProcessingTime
```

### PromQL with Grafana

```promql
# Variable definitions for dashboards
# $datacenter
label_values(up, datacenter)

# $instance
label_values(up{datacenter="$datacenter"}, instance)

# Dashboard query with variables
sum by (instance) (
  rate(http_requests_total{
    datacenter="$datacenter",
    instance=~"$instance"
  }[$__rate_interval])
)

# Multi-value variable support
sum by (endpoint) (
  rate(http_requests_total{
    method=~"$method"  # Supports multiple selections
  }[5m])
)
```

## Alerting Strategies

### KQL Alert Queries

```kql
// High error rate alert
requests
| where timestamp > ago(5m)
| summarize 
    TotalRequests = count(),
    FailedRequests = countif(success == false)
| extend ErrorRate = 100.0 * FailedRequests / TotalRequests
| where ErrorRate > 5 and TotalRequests > 100

// Performance degradation alert
let baseline = toscalar(
    requests
    | where timestamp between (ago(1d) .. ago(1h))
    | summarize percentile(duration, 95)
);
requests
| where timestamp > ago(10m)
| summarize P95 = percentile(duration, 95)
| where P95 > baseline * 1.5

// Anomaly detection alert
requests
| where timestamp > ago(2h)
| make-series RequestRate = count() on timestamp step 5m
| extend (anomalies, score) = series_decompose_anomalies(RequestRate, 2)
| mv-expand timestamp, RequestRate, anomalies, score
| where anomalies != 0
| where timestamp > ago(10m)
```

### PromQL Alert Rules

```yaml
# prometheus.alerts.yml
groups:
  - name: application_alerts
    rules:
      - alert: HighErrorRate
        expr: |
          sum(rate(http_requests_total{status=~"5.."}[5m])) 
          / sum(rate(http_requests_total[5m])) > 0.05
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: "High error rate detected"
          description: "Error rate is {{ $value | humanizePercentage }}"
      
      - alert: HighLatency
        expr: |
          histogram_quantile(0.95,
            sum by (job, le) (
              rate(http_request_duration_seconds_bucket[5m])
            )
          ) > 0.5
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "High latency detected"
          description: "95th percentile latency is {{ $value }}s"
      
      - alert: PodCrashLooping
        expr: |
          increase(kube_pod_container_status_restarts_total[1h]) > 5
        labels:
          severity: critical
        annotations:
          summary: "Pod {{ $labels.pod }} is crash looping"
          description: "Pod has restarted {{ $value }} times in the last hour"
```

## Visualization Best Practices

### KQL Visualization

```kql
// Time series chart
requests
| where timestamp > ago(1h)
| summarize count() by bin(timestamp, 1m)
| render timechart

// Multi-series comparison
requests
| where timestamp > ago(1h)
| summarize count() by bin(timestamp, 5m), success
| render timechart

// Heatmap for patterns
requests
| where timestamp > ago(7d)
| extend Hour = hourofday(timestamp)
| extend DayOfWeek = dayofweek(timestamp)
| summarize AvgDuration = avg(duration) by Hour, DayOfWeek
| render heatmap

// Pie chart for distribution
requests
| where timestamp > ago(1h)
| summarize count() by resultCode
| render piechart
```

### PromQL Visualization

```promql
# Grafana panel queries

# Stacked area chart - request rate by status
sum by (status) (rate(http_requests_total[5m]))

# Heatmap - latency distribution
sum by (le) (rate(http_request_duration_seconds_bucket[5m]))

# Gauge - current value
avg(up{job="my-service"})

# Table - top endpoints by error rate
topk(10,
  sum by (endpoint) (rate(http_requests_total{status=~"5.."}[5m]))
  / sum by (endpoint) (rate(http_requests_total[5m]))
)

# Single stat - SLA percentage
avg_over_time(
  (1 - sum(rate(http_requests_total{status=~"5.."}[5m]))
    / sum(rate(http_requests_total[5m])))[24h:5m]
) * 100
```

## Query Comparison

### Similar Concepts

| Concept | KQL | PromQL |
|---------|-----|--------|
| Filter | `where` | Label selectors `{key="value"}` |
| Time range | `ago(1h)` | `[1h]` |
| Rate calculation | `count() / timespan` | `rate()` |
| Aggregation | `summarize` | `sum by ()` |
| Time buckets | `bin(timestamp, 5m)` | Automatic with `[5m]` |
| Percentiles | `percentile()` | `histogram_quantile()` |
| Join | `join` | Vector matching |
| Null handling | `isnotnull()` | No nulls in Prometheus |

### Query Examples

**Count requests per minute:**

```kql
// KQL
requests
| where timestamp > ago(1h)
| summarize count() by bin(timestamp, 1m)
```

```promql
# PromQL
sum(rate(http_requests_total[1m]))
```

**Error rate calculation:**

```kql
// KQL
requests
| where timestamp > ago(5m)
| summarize 
    ErrorRate = 100.0 * countif(success == false) / count()
```

```promql
# PromQL
sum(rate(http_requests_total{status=~"5.."}[5m])) 
  / sum(rate(http_requests_total[5m])) * 100
```

**95th percentile latency:**

```kql
// KQL
requests
| where timestamp > ago(5m)
| summarize percentile(duration, 95) by name
```

```promql
# PromQL
histogram_quantile(0.95,
  sum by (endpoint, le) (
    rate(http_request_duration_seconds_bucket[5m])
  )
)
```

## Conclusion

Both KQL and PromQL are powerful query languages optimized for their respective domains:

### KQL Strengths
- Rich string manipulation
- Complex event correlation
- Built-in ML functions
- Flexible time operations
- Ideal for logs and traces

### PromQL Strengths
- Optimized for metrics
- Efficient time series operations
- Built-in rate calculations
- Recording rules for performance
- Ideal for real-time monitoring

### Best Practices
1. **Learn the fundamentals** thoroughly before advanced features
2. **Optimize queries** for performance from the start
3. **Use appropriate time ranges** for your use case
4. **Build reusable queries** through functions/recording rules
5. **Test alerts** thoroughly before production
6. **Document complex queries** for team understanding
7. **Monitor query performance** in production

The key to mastery is understanding when to use each language and how to write efficient queries that provide actionable insights for your observability needs.