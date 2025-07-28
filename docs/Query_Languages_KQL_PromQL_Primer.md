# Query Languages Primer: KQL, PromQL and Modern Log Querying

## A Comprehensive Guide to Querying Monitoring and Log Data

### Table of Contents
1. [Introduction to Query Languages](#introduction-to-query-languages)
2. [KQL (Kusto Query Language)](#kql-kusto-query-language)
3. [PromQL (Prometheus Query Language)](#promql-prometheus-query-language)
4. [Lucene/Elasticsearch Query DSL](#luceneelasticsearch-query-dsl)
5. [LogQL (Loki Query Language)](#logql-loki-query-language)
6. [SQL for Logs](#sql-for-logs)
7. [Comparison and Best Practices](#comparison-and-best-practices)
8. [Advanced Query Patterns](#advanced-query-patterns)
9. [Performance Optimization](#performance-optimization)
10. [Future of Log Querying](#future-of-log-querying)

## Introduction to Query Languages

Modern observability platforms use specialized query languages optimized for time-series data, logs, and metrics. Understanding these languages is crucial for effective monitoring and troubleshooting.

### Why Specialized Query Languages?

```yaml
Traditional SQL Limitations:
  - Not optimized for time-series data
  - Complex syntax for common monitoring tasks
  - Limited built-in functions for metrics
  - Poor performance on high-cardinality data

Specialized Languages Benefits:
  - Time-series native operations
  - Built-in aggregation functions
  - Optimized for streaming data
  - Domain-specific functions
  - Better performance at scale
```

## KQL (Kusto Query Language)

KQL is Microsoft's query language used in Azure Monitor, Application Insights, and Azure Data Explorer.

### KQL Fundamentals

```kql
// Basic structure: source | operator | operator | ...
requests
| where timestamp > ago(1h)
| summarize count() by bin(timestamp, 5m)
| render timechart

// Key concepts:
// - Pipe-based syntax
// - Schema-aware
// - Rich type system
// - Extensive built-in functions
```

### Essential KQL Operators

```kql
// 1. Filtering
requests
| where duration > 1000
| where success == false
| where name contains "api"
| where customDimensions.userId in ("user1", "user2")

// 2. Projection
requests
| project 
    timestamp,
    name,
    duration,
    userId = tostring(customDimensions.userId)
| extend durationInSeconds = duration / 1000

// 3. Aggregation
requests
| summarize 
    avg_duration = avg(duration),
    p95_duration = percentile(duration, 95),
    request_count = count()
    by bin(timestamp, 5m), name

// 4. Joining
let userSessions = 
    customEvents
    | where name == "SessionStart"
    | project sessionId = tostring(customDimensions.sessionId), userId;
requests
| join kind=inner userSessions on $left.session_Id == $right.sessionId
| project timestamp, name, duration, userId

// 5. Time series analysis
requests
| make-series 
    avg_duration = avg(duration) default=0
    on timestamp 
    from ago(7d) to now() 
    step 1h
    by name
| extend (anomalies, score) = series_decompose_anomalies(avg_duration)
```

### Advanced KQL Patterns

```kql
// 1. Funnel Analysis
let step1 = customEvents | where name == "HomePage" | distinct user_Id;
let step2 = customEvents | where name == "ProductPage" | distinct user_Id;
let step3 = customEvents | where name == "AddToCart" | distinct user_Id;
let step4 = customEvents | where name == "Checkout" | distinct user_Id;
union 
    (step1 | extend Step = "1. HomePage" | count),
    (step2 | extend Step = "2. ProductPage" | count),
    (step3 | extend Step = "3. AddToCart" | count),
    (step4 | extend Step = "4. Checkout" | count)
| project Step, Users = Count

// 2. Cohort Analysis
let startDate = datetime(2024-01-01);
customEvents
| where timestamp >= startDate
| extend Week = floor((timestamp - startDate) / 7d)
| summarize Users = dcount(user_Id) by Week, name
| evaluate pivot(name, sum(Users))

// 3. Anomaly Detection
requests
| where timestamp > ago(24h)
| summarize RequestCount = count() by bin(timestamp, 5m)
| extend anomalies = series_decompose_anomalies(RequestCount, 2.5)
| mv-expand timestamp, RequestCount, anomalies
| where anomalies == 1

// 4. Performance Degradation Detection
let baseline = 
    requests
    | where timestamp between(ago(8d) .. ago(1d))
    | summarize baseline_p95 = percentile(duration, 95) by name;
requests
| where timestamp > ago(1h)
| summarize current_p95 = percentile(duration, 95) by name
| join kind=inner baseline on name
| extend degradation_percent = (current_p95 - baseline_p95) / baseline_p95 * 100
| where degradation_percent > 20
| order by degradation_percent desc

// 5. Custom Metrics from Logs
traces
| where message startswith "METRIC:"
| parse message with "METRIC: " metricName:string " VALUE: " metricValue:double " TAGS: " tags
| extend tagPairs = split(tags, ",")
| mv-expand tagPair = tagPairs
| extend tag = split(tagPair, "=")
| extend tagKey = tostring(tag[0]), tagValue = tostring(tag[1])
| summarize avg(metricValue) by bin(timestamp, 1m), metricName, tagKey, tagValue
```

### KQL Best Practices

```kql
// 1. Use time filters first
// Good
requests
| where timestamp > ago(1h)  // Time filter first
| where duration > 1000

// Bad
requests
| where duration > 1000
| where timestamp > ago(1h)  // Time filter last

// 2. Use summarize before join
// Good
let summary = requests
    | summarize count() by user_Id
    | where count_ > 100;
users
| join kind=inner summary on user_Id

// 3. Use extend for computed columns
requests
| extend 
    durationSeconds = duration / 1000,
    isSlowRequest = duration > 5000,
    hourOfDay = hourofday(timestamp)

// 4. Use let for reusable queries
let errorRequests = requests | where success == false;
let criticalErrors = errorRequests | where resultCode >= 500;
criticalErrors
| summarize ErrorCount = count() by bin(timestamp, 5m)
```

## PromQL (Prometheus Query Language)

PromQL is designed specifically for querying Prometheus time-series data.

### PromQL Fundamentals

```promql
# Basic structure: metric{labels}[time range]

# Instant vector - single value per series
http_requests_total{job="api", status="200"}

# Range vector - multiple values per series
http_requests_total{job="api"}[5m]

# Scalar - single numeric value
42

# String - single string value (limited use)
"api-server"
```

### Essential PromQL Functions

```promql
# 1. Rate and increase
# Rate - per-second average rate
rate(http_requests_total[5m])

# Increase - total increase over time
increase(http_requests_total[1h])

# 2. Aggregation
# Sum across dimensions
sum(rate(http_requests_total[5m])) by (job)

# Average response time
avg(http_request_duration_seconds) by (handler)

# 95th percentile
histogram_quantile(0.95, 
  sum(rate(http_request_duration_seconds_bucket[5m])) by (le, handler)
)

# 3. Math operations
# Error rate
sum(rate(http_requests_total{status=~"5.."}[5m])) 
  / 
sum(rate(http_requests_total[5m]))

# 4. Time-based functions
# Average over time
avg_over_time(up[5m])

# Changes (useful for counters)
changes(process_start_time_seconds[1h])

# 5. Predictions
# Linear regression
predict_linear(node_filesystem_free_bytes[1h], 24*3600)
```

### Advanced PromQL Patterns

```promql
# 1. SLI/SLO Calculations
# Availability SLI
sum(rate(http_requests_total{status!~"5.."}[5m])) 
  / 
sum(rate(http_requests_total[5m])) 
  * 100

# Latency SLO (95th percentile under 500ms)
histogram_quantile(0.95,
  sum(rate(http_request_duration_seconds_bucket{le="0.5"}[5m])) by (le)
) < 0.5

# 2. Capacity Planning
# Disk space exhaustion prediction
predict_linear(
  node_filesystem_avail_bytes{mountpoint="/"}[4h], 
  7*24*3600
) < 0

# CPU saturation trend
predict_linear(
  avg_over_time(
    1 - avg(rate(node_cpu_seconds_total{mode="idle"}[5m]))[30m:]
  )[1h:], 
  3600
)

# 3. Anomaly Detection
# Z-score based anomaly
(
  rate(http_requests_total[5m]) 
    - 
  avg_over_time(rate(http_requests_total[5m])[1h:5m])
) 
  / 
stddev_over_time(rate(http_requests_total[5m])[1h:5m]) 
  > 3

# 4. Service Dependencies
# Error budget burn rate
(
  1 - (
    sum(rate(http_requests_total{status!~"5.."}[5m])) by (service)
      /
    sum(rate(http_requests_total[5m])) by (service)
  )
) * 43800 # minutes in month

# 5. Complex Aggregations
# Top K with others
topk(5, sum(rate(http_requests_total[5m])) by (endpoint))
  or
sum(rate(http_requests_total[5m])) by (endpoint) 
  * 0 
  + on() group_left sum(rate(http_requests_total[5m]))
```

### PromQL Recording Rules

```yaml
# prometheus-rules.yml
groups:
  - name: example
    interval: 30s
    rules:
      # Pre-calculate expensive queries
      - record: job:http_requests:rate5m
        expr: |
          sum(rate(http_requests_total[5m])) by (job)
      
      # Error rates
      - record: job:http_errors:rate5m
        expr: |
          sum(rate(http_requests_total{status=~"5.."}[5m])) by (job)
      
      # SLI metrics
      - record: job:availability:ratio_rate5m
        expr: |
          1 - (job:http_errors:rate5m / job:http_requests:rate5m)
```

## Lucene/Elasticsearch Query DSL

Elasticsearch uses Lucene query syntax and its own Query DSL for complex searches.

### Lucene Query Syntax

```lucene
# Basic text search
error

# Field search
status:500

# Phrase search
"out of memory"

# Boolean operators
status:500 AND response_time:>1000
status:(500 OR 503)
NOT status:200

# Wildcards
host:api-*
error_message:*timeout*

# Range queries
response_time:[1000 TO 5000]
timestamp:[2024-01-01 TO 2024-01-31]
status:[400 TO 499]

# Fuzzy search
kubernetes~  # matches kubernetes, kubernates, etc.

# Proximity search
"database error"~5  # words within 5 positions
```

### Elasticsearch Query DSL

```json
// 1. Match Query
{
  "query": {
    "match": {
      "message": "error exception"
    }
  }
}

// 2. Bool Query
{
  "query": {
    "bool": {
      "must": [
        { "term": { "status": 500 } },
        { "range": { "response_time": { "gte": 1000 } } }
      ],
      "filter": [
        { "term": { "environment": "production" } },
        { "range": { "@timestamp": { "gte": "now-1h" } } }
      ],
      "must_not": [
        { "term": { "user": "healthcheck" } }
      ]
    }
  }
}

// 3. Aggregations
{
  "size": 0,
  "query": {
    "range": { "@timestamp": { "gte": "now-1h" } }
  },
  "aggs": {
    "errors_over_time": {
      "date_histogram": {
        "field": "@timestamp",
        "interval": "5m"
      },
      "aggs": {
        "error_count": {
          "filter": { "term": { "level": "ERROR" } }
        }
      }
    },
    "top_errors": {
      "terms": {
        "field": "error_message.keyword",
        "size": 10
      }
    }
  }
}

// 4. Pipeline Aggregations
{
  "aggs": {
    "sales_per_month": {
      "date_histogram": {
        "field": "@timestamp",
        "interval": "month"
      },
      "aggs": {
        "total_sales": { "sum": { "field": "amount" } },
        "sales_derivative": {
          "derivative": { "buckets_path": "total_sales" }
        }
      }
    }
  }
}
```

## LogQL (Loki Query Language)

LogQL is Grafana Loki's query language, inspired by PromQL but designed for logs.

### LogQL Fundamentals

```logql
# Basic log stream selector
{app="puzzle-api"}

# Multiple labels
{app="puzzle-api", env="production"}

# Label matching
{app=~"puzzle-.*", level="error"}

# Line filter expressions
{app="puzzle-api"} |= "error"
{app="puzzle-api"} |~ "error|ERROR"
{app="puzzle-api"} != "healthcheck"

# JSON parsing
{app="puzzle-api"} 
  | json 
  | line_format "{{.timestamp}} {{.level}} {{.message}}"
```

### LogQL Advanced Queries

```logql
# 1. Log metrics
rate({app="puzzle-api", level="error"}[5m])

# 2. Aggregations
sum(rate({app="puzzle-api"} |= "error" [5m])) by (instance)

# 3. Label extraction
{app="puzzle-api"}
  | regexp "(?P<method>\\w+) (?P<path>[^ ]+) (?P<status>\\d+)"
  | line_format "{{.method}} {{.path}} returned {{.status}}"

# 4. JSON field filtering
{app="puzzle-api"}
  | json
  | response_time > 1000
  | line_format "Slow request: {{.request_id}} took {{.response_time}}ms"

# 5. Pattern matching
sum by (level) (
  count_over_time({app="puzzle-api"} 
    | pattern "<_> <level> <_>" [5m]
  )
)
```

## SQL for Logs

Some platforms (ClickHouse, AWS Athena, BigQuery) use SQL for log analysis.

### ClickHouse Example

```sql
-- Basic log analysis
SELECT 
    toStartOfMinute(timestamp) as minute,
    level,
    COUNT(*) as count
FROM logs
WHERE timestamp >= now() - INTERVAL 1 HOUR
GROUP BY minute, level
ORDER BY minute DESC;

-- Pattern detection
WITH patterns AS (
    SELECT 
        extractAllGroupsVertical(message, '(\w+Exception)')[1] as exception_type,
        COUNT(*) as count
    FROM logs
    WHERE level = 'ERROR'
        AND timestamp >= now() - INTERVAL 24 HOUR
    GROUP BY exception_type
)
SELECT * FROM patterns
WHERE count > 10
ORDER BY count DESC;

-- Response time percentiles
SELECT
    quantiles(0.5, 0.95, 0.99)(response_time) as percentiles,
    endpoint
FROM logs
WHERE timestamp >= now() - INTERVAL 1 HOUR
GROUP BY endpoint
HAVING count() > 100
ORDER BY percentiles[2] DESC;
```

## Comparison and Best Practices

### Language Comparison Matrix

| Feature | KQL | PromQL | Lucene/ES | LogQL | SQL |
|---------|-----|---------|-----------|-------|-----|
| **Time-series native** | ✓ | ✓✓✓ | ✓ | ✓✓ | ✓ |
| **Full-text search** | ✓✓ | ✗ | ✓✓✓ | ✓✓ | ✓ |
| **Learning curve** | Medium | Steep | Low/Medium | Medium | Low |
| **Performance** | ✓✓✓ | ✓✓✓ | ✓✓ | ✓✓ | ✓✓✓ |
| **Aggregations** | ✓✓✓ | ✓✓ | ✓✓✓ | ✓✓ | ✓✓✓ |
| **Joins** | ✓✓ | ✗ | ✓ | ✗ | ✓✓✓ |
| **Machine Learning** | ✓✓ | ✗ | ✓ | ✗ | ✓ |

### Choosing the Right Language

```yaml
Use KQL When:
  - Using Azure ecosystem
  - Need rich analytics functions
  - Complex event correlation
  - Machine learning integration

Use PromQL When:
  - Metrics-focused monitoring
  - SLI/SLO calculations
  - Kubernetes monitoring
  - Capacity planning

Use Elasticsearch When:
  - Full-text log search
  - Complex document queries
  - Need distributed search
  - Mixed structured/unstructured data

Use LogQL When:
  - Grafana-centric stack
  - Simple log aggregation
  - Cost-conscious logging
  - Kubernetes native

Use SQL When:
  - Team knows SQL
  - Complex analytical queries
  - Data warehouse integration
  - Business intelligence needs
```

## Advanced Query Patterns

### Cross-Language Patterns

```yaml
Pattern: Error Rate Calculation
  KQL: |
    requests
    | summarize 
        errors = countif(success == false),
        total = count()
    | extend error_rate = errors * 100.0 / total
  
  PromQL: |
    sum(rate(http_requests_total{status=~"5.."}[5m]))
      /
    sum(rate(http_requests_total[5m]))
    * 100
  
  LogQL: |
    sum(rate({app="api"} |= "ERROR"[5m]))
      /
    sum(rate({app="api"}[5m]))
    * 100

Pattern: Top N with Others
  KQL: |
    requests
    | summarize count() by endpoint
    | top 5 by count_
    | union (
        requests
        | summarize count() by endpoint
        | top-nested of endpoint by count_ > 5
        | summarize others = sum(count_)
        | extend endpoint = "Others"
    )
  
  PromQL: |
    topk(5, sum by (endpoint) (rate(requests[5m])))
      or
    sum(sum by (endpoint) (rate(requests[5m]))) * 0 + 
      on() group_left 
    (sum(rate(requests[5m])) - sum(topk(5, sum by (endpoint) (rate(requests[5m])))))
```

### Performance Optimization Techniques

```yaml
General Optimization:
  1. Time filtering:
     - Always filter by time first
     - Use appropriate time ranges
     - Avoid scanning all data
  
  2. Field selection:
     - Project only needed fields
     - Avoid wildcard selections
     - Use columnar storage benefits
  
  3. Aggregation pushdown:
     - Aggregate early in pipeline
     - Reduce data movement
     - Use pre-aggregated data
  
  4. Index usage:
     - Create appropriate indexes
     - Use index-friendly queries
     - Monitor index performance

Query-Specific:
  KQL:
    - Use materialized views
    - Leverage shuffle strategy
    - Use update policies
  
  PromQL:
    - Use recording rules
    - Optimize label cardinality
    - Tune retention policies
  
  Elasticsearch:
    - Use filter context
    - Optimize shard allocation
    - Use runtime fields sparingly
```

## Future of Log Querying

### Emerging Trends

```yaml
AI-Powered Querying:
  - Natural language to query
  - Automatic query optimization
  - Anomaly detection built-in
  - Pattern learning

Unified Query Languages:
  - SQL becoming standard
  - Cross-platform compatibility
  - Federation capabilities
  - Standard query APIs

Performance Innovations:
  - Columnar storage adoption
  - GPU acceleration
  - Distributed query planning
  - Smart caching strategies

Developer Experience:
  - Visual query builders
  - Query recommendation
  - Automatic indexing
  - Cost prediction
```

### Next-Generation Platforms

```yaml
Apache Druid:
  - Real-time analytics
  - Sub-second queries
  - High concurrency
  - Time-series optimized

ClickHouse:
  - Columnar storage
  - SQL with extensions
  - Massive scalability
  - Cost efficient

Apache Pinot:
  - Real-time OLAP
  - Ultra-low latency
  - Pluggable indexing
  - Multi-tenancy

DuckDB:
  - In-process OLAP
  - Parquet native
  - Zero dependencies
  - PostgreSQL compatible
```

## Best Practices Summary

```yaml
Query Writing:
  1. Start simple, iterate
  2. Use time bounds always
  3. Test on small datasets
  4. Monitor query performance
  5. Document complex queries

Performance:
  1. Pre-aggregate when possible
  2. Use appropriate retention
  3. Index strategically
  4. Cache query results
  5. Monitor resource usage

Maintenance:
  1. Version control queries
  2. Create query libraries
  3. Standardize patterns
  4. Regular optimization
  5. Train team members

Security:
  1. Implement query limits
  2. Use read-only access
  3. Audit query usage
  4. Sanitize user input
  5. Monitor abnormal patterns
```