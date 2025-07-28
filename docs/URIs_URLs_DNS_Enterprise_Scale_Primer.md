# URIs, URLs, and DNS in Enterprise Scale Systems Primer
## Architecting Robust Web Resource Management

### Executive Summary

Understanding URIs, URLs, and DNS is fundamental for building enterprise-scale distributed systems. This primer covers the intricacies of web resource identification, naming strategies, DNS architecture, and enterprise patterns for managing millions of resources across global infrastructure.

## Table of Contents

1. [URI and URL Fundamentals](#uri-and-url-fundamentals)
2. [DNS Architecture](#dns-architecture)
3. [Enterprise URL Design](#enterprise-url-design)
4. [Microservices and Service Discovery](#microservices-and-service-discovery)
5. [Load Balancing and Traffic Management](#load-balancing-and-traffic-management)
6. [Security Considerations](#security-considerations)
7. [Performance Optimization](#performance-optimization)
8. [Multi-Region Strategies](#multi-region-strategies)
9. [Monitoring and Analytics](#monitoring-and-analytics)
10. [Best Practices](#best-practices)

## URI and URL Fundamentals

### URI vs URL vs URN

```
URI (Uniform Resource Identifier)
├── URL (Uniform Resource Locator) - How to access
└── URN (Uniform Resource Name) - What it is

Examples:
URL: https://api.example.com/v2/users/123
URN: urn:isbn:0-486-27557-4
URI: Both of the above
```

### Anatomy of a URL

```
https://subdomain.example.com:8443/api/v2/users/123?active=true&role=admin#section2
└─┬─┘   └───┬───┘ └───┬────┘ └┬─┘ └──────┬──────┘ └──────┬───────┘ └───┬───┘
scheme  subdomain   domain   port      path            query         fragment

Components:
- Scheme/Protocol: https, http, ftp, ws, wss
- Authority: subdomain.example.com:8443
- Host: subdomain.example.com
- Port: 8443 (defaults: http=80, https=443)
- Path: /api/v2/users/123
- Query: ?active=true&role=admin
- Fragment: #section2
```

### URL Encoding

```javascript
// JavaScript URL encoding
const userInput = "John Doe & Co.";
const encoded = encodeURIComponent(userInput);
// Result: "John%20Doe%20%26%20Co."

const url = `https://api.example.com/search?q=${encoded}`;

// Full URL encoding
const fullUrl = new URL("https://api.example.com/search");
fullUrl.searchParams.append("q", "John Doe & Co.");
fullUrl.searchParams.append("category", "users & groups");
// Result: https://api.example.com/search?q=John+Doe+%26+Co.&category=users+%26+groups
```

### Reserved and Unreserved Characters

```
Reserved Characters (must be encoded):
! * ' ( ) ; : @ & = + $ , / ? # [ ]

Unreserved Characters (safe to use):
A-Z a-z 0-9 - _ . ~

Common Encodings:
Space → %20 or +
& → %26
# → %23
/ → %2F
? → %3F
= → %3D
```

## DNS Architecture

### DNS Hierarchy

```
                    . (root)
                    |
    ┌───────────────┼───────────────┐
    |               |               |
   .com           .org            .net
    |               |               |
example.com    example.org     example.net
    |
    ├── www.example.com
    ├── api.example.com
    └── admin.example.com
```

### DNS Record Types

```yaml
# A Record - IPv4 Address
api.example.com.    IN    A    192.168.1.100

# AAAA Record - IPv6 Address
api.example.com.    IN    AAAA    2001:0db8:85a3::8a2e:0370:7334

# CNAME Record - Canonical Name (Alias)
www.example.com.    IN    CNAME    example.com.

# MX Record - Mail Exchange
example.com.        IN    MX    10    mail1.example.com.
example.com.        IN    MX    20    mail2.example.com.

# TXT Record - Text Information
example.com.        IN    TXT    "v=spf1 include:_spf.google.com ~all"

# SRV Record - Service Location
_service._proto.example.com.    IN    SRV    priority weight port target

# CAA Record - Certificate Authority Authorization
example.com.        IN    CAA    0 issue "letsencrypt.org"
```

### Enterprise DNS Architecture

```
┌─────────────────────────────────────────────────────────┐
│                   Global Load Balancer                   │
│                    (Anycast DNS)                        │
└─────────────┬─────────────────────┬────────────────────┘
              │                     │
    ┌─────────▼──────────┐ ┌───────▼──────────┐
    │ GeoDNS Provider 1  │ │ GeoDNS Provider 2 │
    │  (Route 53, etc)   │ │   (CloudFlare)    │
    └─────────┬──────────┘ └───────┬──────────┘
              │                     │
    ┌─────────▼──────────┐ ┌───────▼──────────┐
    │  Regional DNS      │ │  Regional DNS     │
    │  (US-East)         │ │  (EU-West)        │
    └─────────┬──────────┘ └───────┬──────────┘
              │                     │
    ┌─────────▼──────────┐ ┌───────▼──────────┐
    │ Internal DNS       │ │ Internal DNS      │
    │ (service.internal) │ │ (service.internal)│
    └────────────────────┘ └───────────────────┘
```

### DNS Resolution Flow

```csharp
// .NET DNS resolution example
public class DnsResolver
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<DnsResolver> _logger;
    
    public async Task<IPAddress[]> ResolveAsync(string hostname)
    {
        // Check local cache first
        if (_cache.TryGetValue($"dns:{hostname}", out IPAddress[] cached))
        {
            return cached;
        }
        
        try
        {
            // Perform DNS lookup
            var addresses = await Dns.GetHostAddressesAsync(hostname);
            
            // Cache with TTL
            _cache.Set($"dns:{hostname}", addresses, TimeSpan.FromMinutes(5));
            
            _logger.LogInformation($"Resolved {hostname} to {addresses.Length} addresses");
            return addresses;
        }
        catch (SocketException ex)
        {
            _logger.LogError(ex, $"Failed to resolve {hostname}");
            throw new DnsResolutionException($"Cannot resolve {hostname}", ex);
        }
    }
}
```

## Enterprise URL Design

### RESTful URL Patterns

```yaml
# Resource-based URLs
GET    /api/v2/users              # List users
POST   /api/v2/users              # Create user
GET    /api/v2/users/123          # Get specific user
PUT    /api/v2/users/123          # Update user
DELETE /api/v2/users/123          # Delete user

# Nested resources
GET    /api/v2/users/123/orders   # User's orders
POST   /api/v2/users/123/orders   # Create order for user
GET    /api/v2/orders/456         # Get specific order

# Query patterns
GET    /api/v2/users?role=admin&active=true&page=2&limit=50
GET    /api/v2/users?sort=created_at:desc,name:asc
GET    /api/v2/users?fields=id,name,email
GET    /api/v2/users?q=john&filter[role]=admin
```

### URL Versioning Strategies

```yaml
# Path versioning (most common)
https://api.example.com/v1/users
https://api.example.com/v2/users

# Subdomain versioning
https://v1.api.example.com/users
https://v2.api.example.com/users

# Query parameter versioning
https://api.example.com/users?version=1
https://api.example.com/users?version=2

# Header versioning
GET /users HTTP/1.1
Host: api.example.com
API-Version: v2

# Content negotiation
GET /users HTTP/1.1
Host: api.example.com
Accept: application/vnd.example.v2+json
```

### URL Design for Microservices

```nginx
# Nginx routing configuration
upstream user_service {
    server user-service-1.internal:8080;
    server user-service-2.internal:8080;
}

upstream order_service {
    server order-service-1.internal:8080;
    server order-service-2.internal:8080;
}

server {
    listen 443 ssl;
    server_name api.example.com;
    
    # Route by path prefix
    location /api/v2/users {
        proxy_pass http://user_service;
    }
    
    location /api/v2/orders {
        proxy_pass http://order_service;
    }
    
    # Route by subdomain
    # users.api.example.com → user_service
    # orders.api.example.com → order_service
}
```

### Slug Generation for SEO-Friendly URLs

```csharp
public static class SlugGenerator
{
    public static string GenerateSlug(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;
        
        // Convert to lowercase
        var slug = input.ToLowerInvariant();
        
        // Remove accents/diacritics
        slug = RemoveDiacritics(slug);
        
        // Replace spaces and special characters with hyphens
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"\s+", "-");
        slug = Regex.Replace(slug, @"-+", "-");
        
        // Trim hyphens from ends
        slug = slug.Trim('-');
        
        // Limit length
        if (slug.Length > 100)
            slug = slug.Substring(0, 100).TrimEnd('-');
        
        return slug;
    }
    
    // Example usage:
    // "John's Café & Restaurant!" → "johns-cafe-restaurant"
    // "Hello    World!!!" → "hello-world"
}
```

## Microservices and Service Discovery

### Service Discovery Patterns

```yaml
# Consul service definition
services:
  - name: user-service
    tags:
      - api
      - v2
    port: 8080
    check:
      http: http://localhost:8080/health
      interval: 10s
    meta:
      version: "2.1.0"
      protocol: "http"
```

### Internal vs External URLs

```csharp
public class ServiceUrlResolver
{
    private readonly IServiceDiscovery _discovery;
    private readonly IConfiguration _config;
    
    public async Task<string> ResolveServiceUrl(
        string serviceName, 
        bool isInternal = true)
    {
        if (isInternal)
        {
            // Internal service discovery
            var instances = await _discovery.GetHealthyInstances(serviceName);
            var instance = SelectInstance(instances);
            return $"http://{instance.Address}:{instance.Port}";
        }
        else
        {
            // External URL from configuration
            return _config[$"Services:{serviceName}:ExternalUrl"];
        }
    }
    
    // Example configurations:
    // Internal: http://user-service.internal:8080
    // External: https://api.example.com/users
}
```

### API Gateway URL Routing

```yaml
# Kong/API Gateway configuration
routes:
  - name: user-service-route
    paths:
      - /api/v2/users
    service: user-service
    plugins:
      - name: rate-limiting
        config:
          minute: 1000
      - name: jwt
        
  - name: order-service-route
    paths:
      - /api/v2/orders
    service: order-service
    strip_path: true  # Remove /api/v2/orders prefix
```

## Load Balancing and Traffic Management

### Geographic Load Balancing

```yaml
# AWS Route 53 Geolocation routing
resource "aws_route53_record" "api" {
  zone_id = aws_route53_zone.main.zone_id
  name    = "api.example.com"
  type    = "A"
  
  set_identifier = "us-east"
  geolocation_routing_policy {
    continent = "NA"
  }
  
  alias {
    name    = aws_lb.us_east.dns_name
    zone_id = aws_lb.us_east.zone_id
  }
}

resource "aws_route53_record" "api_eu" {
  zone_id = aws_route53_zone.main.zone_id
  name    = "api.example.com"
  type    = "A"
  
  set_identifier = "eu-west"
  geolocation_routing_policy {
    continent = "EU"
  }
  
  alias {
    name    = aws_lb.eu_west.dns_name
    zone_id = aws_lb.eu_west.zone_id
  }
}
```

### Blue-Green Deployments with DNS

```python
# DNS-based blue-green deployment
import boto3
import time

def switch_to_green(hosted_zone_id, domain_name):
    route53 = boto3.client('route53')
    
    # Get current record
    response = route53.list_resource_record_sets(
        HostedZoneId=hosted_zone_id,
        StartRecordName=domain_name
    )
    
    # Update weighted routing
    changes = [{
        'Action': 'UPSERT',
        'ResourceRecordSet': {
            'Name': domain_name,
            'Type': 'CNAME',
            'SetIdentifier': 'green',
            'Weight': 100,  # 100% to green
            'TTL': 60,
            'ResourceRecords': [{'Value': 'green.example.com'}]
        }
    }, {
        'Action': 'UPSERT',
        'ResourceRecordSet': {
            'Name': domain_name,
            'Type': 'CNAME',
            'SetIdentifier': 'blue',
            'Weight': 0,   # 0% to blue
            'TTL': 60,
            'ResourceRecords': [{'Value': 'blue.example.com'}]
        }
    }]
    
    route53.change_resource_record_sets(
        HostedZoneId=hosted_zone_id,
        ChangeBatch={'Changes': changes}
    )
```

### Traffic Splitting

```nginx
# Nginx split testing configuration
split_clients "${remote_addr}${remote_port}" $variant {
    20%     "v2";
    *       "v1";
}

server {
    location /api/ {
        if ($variant = "v2") {
            proxy_pass http://api-v2-backend;
        }
        proxy_pass http://api-v1-backend;
    }
}
```

## Security Considerations

### URL Security Best Practices

```csharp
public class SecureUrlBuilder
{
    private readonly IDataProtector _protector;
    
    public string GenerateSecureUrl(string resource, TimeSpan expiry)
    {
        var token = new SecurityToken
        {
            Resource = resource,
            Expiry = DateTime.UtcNow.Add(expiry),
            Nonce = Guid.NewGuid()
        };
        
        var encrypted = _protector.Protect(JsonSerializer.Serialize(token));
        var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(encrypted));
        
        return $"https://api.example.com/{resource}?token={encoded}";
    }
    
    public bool ValidateSecureUrl(string url)
    {
        var uri = new Uri(url);
        var token = HttpUtility.ParseQueryString(uri.Query)["token"];
        
        try
        {
            var decoded = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
            var decrypted = _protector.Unprotect(decoded);
            var securityToken = JsonSerializer.Deserialize<SecurityToken>(decrypted);
            
            return securityToken.Expiry > DateTime.UtcNow;
        }
        catch
        {
            return false;
        }
    }
}
```

### CORS Configuration

```csharp
// ASP.NET Core CORS configuration
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("Production", builder =>
            {
                builder
                    .WithOrigins(
                        "https://app.example.com",
                        "https://www.example.com"
                    )
                    .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
                    .WithHeaders("Authorization", "Content-Type", "X-Api-Version")
                    .WithExposedHeaders("X-Total-Count", "X-Page-Number")
                    .SetPreflightMaxAge(TimeSpan.FromHours(24))
                    .AllowCredentials();
            });
        });
    }
}
```

### DNSSEC Implementation

```bash
# Enable DNSSEC for domain
# 1. Generate KSK (Key Signing Key)
dnssec-keygen -a RSASHA256 -b 2048 -f KSK example.com

# 2. Generate ZSK (Zone Signing Key)  
dnssec-keygen -a RSASHA256 -b 1024 example.com

# 3. Sign the zone
dnssec-signzone -o example.com -k Kexample.com.+008+12345.key \
    example.com.zone Kexample.com.+008+54321.key

# 4. Update parent zone with DS record
example.com. IN DS 12345 8 2 [digest]
```

## Performance Optimization

### DNS Caching Strategies

```yaml
# DNS caching layers
Browser Cache:
  - Duration: 60-300 seconds
  - Controlled by: TTL records

OS Cache:
  - Windows: ipconfig /displaydns
  - Linux: systemd-resolved
  - macOS: dscacheutil -statistics

Application Cache:
  - In-memory cache
  - Redis/Memcached
  - Custom TTL logic

CDN Cache:
  - CloudFlare: 300 seconds default
  - CloudFront: 86400 seconds default
  - Akamai: Configurable
```

### Connection Pooling

```csharp
public class HttpClientFactory
{
    private readonly ConcurrentDictionary<string, HttpClient> _clients;
    private readonly IHttpClientFactory _factory;
    
    public HttpClient GetClient(string baseUrl)
    {
        return _clients.GetOrAdd(baseUrl, url =>
        {
            var uri = new Uri(url);
            var client = _factory.CreateClient(uri.Host);
            
            // Configure connection pooling
            var handler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(15),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
                MaxConnectionsPerServer = 100,
                EnableMultipleHttp2Connections = true
            };
            
            return new HttpClient(handler) { BaseAddress = uri };
        });
    }
}
```

### URL Shortening for Performance

```csharp
public class UrlShortener
{
    private readonly IDistributedCache _cache;
    private readonly IDbContext _db;
    
    public async Task<string> ShortenUrl(string longUrl)
    {
        // Generate short code
        var hash = ComputeHash(longUrl);
        var shortCode = Base62Encode(hash);
        
        // Store mapping
        await _cache.SetStringAsync(
            $"url:{shortCode}", 
            longUrl,
            new DistributedCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromDays(7)
            });
        
        // Persist to database
        await _db.UrlMappings.AddAsync(new UrlMapping
        {
            ShortCode = shortCode,
            LongUrl = longUrl,
            CreatedAt = DateTime.UtcNow,
            Hits = 0
        });
        
        return $"https://short.example.com/{shortCode}";
    }
    
    public async Task<string> ExpandUrl(string shortCode)
    {
        // Check cache first
        var cached = await _cache.GetStringAsync($"url:{shortCode}");
        if (cached != null)
        {
            return cached;
        }
        
        // Fallback to database
        var mapping = await _db.UrlMappings
            .FirstOrDefaultAsync(m => m.ShortCode == shortCode);
            
        if (mapping != null)
        {
            // Update cache
            await _cache.SetStringAsync($"url:{shortCode}", mapping.LongUrl);
            
            // Increment hit counter asynchronously
            _ = Task.Run(() => IncrementHitCounter(shortCode));
            
            return mapping.LongUrl;
        }
        
        return null;
    }
}
```

## Multi-Region Strategies

### Global URL Architecture

```yaml
# Multi-region URL strategy
Primary Domain: example.com
  ├── Global CDN: cdn.example.com
  ├── Regional APIs:
  │   ├── api-us.example.com
  │   ├── api-eu.example.com
  │   └── api-asia.example.com
  └── Services:
      ├── auth.example.com (global)
      └── media.example.com (regional)

# Latency-based routing
Route 53 / Traffic Manager:
  - Measure latency from multiple regions
  - Route to fastest endpoint
  - Automatic failover
```

### Cross-Region Replication

```python
# URL metadata replication
class UrlMetadataReplicator:
    def __init__(self, regions):
        self.regions = regions
        self.primary_region = regions[0]
        
    async def replicate_url_config(self, url_config):
        # Write to primary region first
        await self.write_to_region(self.primary_region, url_config)
        
        # Replicate to other regions asynchronously
        tasks = []
        for region in self.regions[1:]:
            task = asyncio.create_task(
                self.write_to_region(region, url_config)
            )
            tasks.append(task)
        
        # Wait for all replications
        results = await asyncio.gather(*tasks, return_exceptions=True)
        
        # Handle failures
        failed_regions = [
            region for region, result in zip(self.regions[1:], results)
            if isinstance(result, Exception)
        ]
        
        if failed_regions:
            await self.queue_retry(failed_regions, url_config)
```

## Monitoring and Analytics

### URL Analytics

```sql
-- URL performance analytics
CREATE TABLE url_metrics (
    id BIGSERIAL PRIMARY KEY,
    url_pattern VARCHAR(500),
    timestamp TIMESTAMP,
    response_time_ms INT,
    status_code INT,
    user_agent VARCHAR(500),
    geo_country VARCHAR(2),
    geo_region VARCHAR(50)
);

-- Analyze URL patterns
WITH url_stats AS (
    SELECT 
        url_pattern,
        COUNT(*) as requests,
        AVG(response_time_ms) as avg_response_time,
        PERCENTILE_CONT(0.95) WITHIN GROUP (ORDER BY response_time_ms) as p95_response_time,
        SUM(CASE WHEN status_code >= 500 THEN 1 ELSE 0 END)::FLOAT / COUNT(*) as error_rate
    FROM url_metrics
    WHERE timestamp > NOW() - INTERVAL '1 hour'
    GROUP BY url_pattern
)
SELECT * FROM url_stats
WHERE requests > 100
ORDER BY p95_response_time DESC;
```

### DNS Monitoring

```python
# DNS health monitoring
import dns.resolver
import time
from prometheus_client import Histogram, Counter

dns_lookup_time = Histogram('dns_lookup_duration_seconds', 
                           'DNS lookup duration',
                           ['domain', 'record_type'])
dns_lookup_errors = Counter('dns_lookup_errors_total',
                           'DNS lookup errors',
                           ['domain', 'error_type'])

def monitor_dns_health(domains):
    resolver = dns.resolver.Resolver()
    resolver.timeout = 5
    resolver.lifetime = 10
    
    for domain in domains:
        for record_type in ['A', 'AAAA', 'CNAME']:
            try:
                start = time.time()
                answers = resolver.resolve(domain, record_type)
                duration = time.time() - start
                
                dns_lookup_time.labels(
                    domain=domain, 
                    record_type=record_type
                ).observe(duration)
                
                # Log results
                logger.info(f"DNS {record_type} for {domain}: {len(answers)} records in {duration:.3f}s")
                
            except dns.resolver.NXDOMAIN:
                dns_lookup_errors.labels(
                    domain=domain,
                    error_type='NXDOMAIN'
                ).inc()
            except dns.resolver.Timeout:
                dns_lookup_errors.labels(
                    domain=domain,
                    error_type='Timeout'
                ).inc()
            except Exception as e:
                dns_lookup_errors.labels(
                    domain=domain,
                    error_type=type(e).__name__
                ).inc()
```

### URL Health Checks

```csharp
public class UrlHealthChecker
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UrlHealthChecker> _logger;
    private readonly IMetrics _metrics;
    
    public async Task<HealthCheckResult> CheckUrlHealth(string url)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var response = await _httpClient.GetAsync(url);
            stopwatch.Stop();
            
            _metrics.Measure.Histogram.Update(
                "url_health_check_duration",
                stopwatch.ElapsedMilliseconds,
                new MetricTags("url", url, "status", ((int)response.StatusCode).ToString())
            );
            
            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy($"URL {url} is healthy");
            }
            else
            {
                return HealthCheckResult.Degraded($"URL {url} returned {response.StatusCode}");
            }
        }
        catch (HttpRequestException ex)
        {
            _metrics.Measure.Counter.Increment(
                "url_health_check_failures",
                new MetricTags("url", url, "error", ex.GetType().Name)
            );
            
            return HealthCheckResult.Unhealthy($"URL {url} is unreachable", ex);
        }
    }
}
```

## Best Practices

### URL Design Guidelines

```yaml
Best Practices:
  Consistency:
    - Use lowercase: /api/users not /API/Users
    - Use hyphens: /api/user-profiles not /api/user_profiles
    - Plural resources: /api/users not /api/user
    
  Versioning:
    - Include version in path: /api/v2/users
    - Use semantic versioning: v1, v2, not v1.0, v2.0
    - Maintain backward compatibility
    
  Security:
    - No sensitive data in URLs
    - Use HTTPS everywhere
    - Implement rate limiting
    - Validate all inputs
    
  Performance:
    - Keep URLs short
    - Use CDN for static assets
    - Implement caching headers
    - Minimize redirects
```

### DNS Best Practices

```yaml
DNS Configuration:
  TTL Values:
    - A/AAAA records: 300-3600 seconds
    - CNAME records: 3600-86400 seconds
    - MX records: 3600-86400 seconds
    - During changes: 60-300 seconds
    
  Redundancy:
    - Multiple NS records
    - Secondary DNS providers
    - Geographic distribution
    - Health checking
    
  Security:
    - Enable DNSSEC
    - Use CAA records
    - Monitor for hijacking
    - Regular audits
```

### Enterprise Patterns

```csharp
// URL builder pattern for microservices
public interface IUrlBuilder
{
    IUrlBuilder WithBaseUrl(string baseUrl);
    IUrlBuilder WithPath(params string[] segments);
    IUrlBuilder WithQueryParam(string key, string value);
    IUrlBuilder WithVersion(string version);
    string Build();
}

public class EnterpriseUrlBuilder : IUrlBuilder
{
    private readonly UriBuilder _uriBuilder;
    private readonly NameValueCollection _queryParams;
    private string _version;
    
    public EnterpriseUrlBuilder()
    {
        _uriBuilder = new UriBuilder();
        _queryParams = HttpUtility.ParseQueryString(string.Empty);
    }
    
    public IUrlBuilder WithBaseUrl(string baseUrl)
    {
        var uri = new Uri(baseUrl);
        _uriBuilder.Scheme = uri.Scheme;
        _uriBuilder.Host = uri.Host;
        _uriBuilder.Port = uri.Port;
        return this;
    }
    
    public IUrlBuilder WithPath(params string[] segments)
    {
        _uriBuilder.Path = string.Join("/", segments.Select(s => s.Trim('/')));
        return this;
    }
    
    public IUrlBuilder WithQueryParam(string key, string value)
    {
        _queryParams[key] = value;
        return this;
    }
    
    public IUrlBuilder WithVersion(string version)
    {
        _version = version;
        return this;
    }
    
    public string Build()
    {
        if (!string.IsNullOrEmpty(_version))
        {
            var segments = _uriBuilder.Path.Split('/');
            segments[0] = _version;
            _uriBuilder.Path = string.Join("/", segments);
        }
        
        _uriBuilder.Query = _queryParams.ToString();
        return _uriBuilder.ToString();
    }
}

// Usage
var url = new EnterpriseUrlBuilder()
    .WithBaseUrl("https://api.example.com")
    .WithVersion("v2")
    .WithPath("users", userId, "orders")
    .WithQueryParam("status", "active")
    .WithQueryParam("limit", "50")
    .Build();
// Result: https://api.example.com/v2/users/123/orders?status=active&limit=50
```

### Troubleshooting Common Issues

```bash
# DNS troubleshooting commands
# Check DNS resolution
nslookup api.example.com
dig api.example.com +trace
host -v api.example.com

# Check DNS propagation
dig @8.8.8.8 example.com
dig @1.1.1.1 example.com

# Check DNSSEC
dig +dnssec example.com

# Test from different locations
curl -I https://api.example.com --resolve api.example.com:443:192.168.1.100

# Trace route to identify network issues
traceroute api.example.com
mtr api.example.com

# Check SSL/TLS
openssl s_client -connect api.example.com:443 -servername api.example.com
```

## Conclusion

Managing URIs, URLs, and DNS at enterprise scale requires careful planning and robust architecture:

### Key Takeaways
1. **Design URLs thoughtfully** - They are a permanent API contract
2. **Plan for scale** - DNS and URL architecture must handle growth
3. **Implement redundancy** - Multiple DNS providers and regions
4. **Monitor everything** - URL performance impacts user experience
5. **Security first** - HTTPS, DNSSEC, and input validation
6. **Cache strategically** - Balance performance with freshness
7. **Version properly** - Plan for API evolution
8. **Document patterns** - Consistency across teams

### Architecture Principles
- **Simplicity**: Keep URLs clean and predictable
- **Scalability**: Design for millions of requests
- **Reliability**: Multiple layers of redundancy
- **Security**: Defense in depth approach
- **Performance**: Optimize at every layer
- **Maintainability**: Clear patterns and documentation

The combination of well-designed URLs, robust DNS infrastructure, and proper monitoring creates a foundation for reliable enterprise-scale systems that can serve billions of requests globally.