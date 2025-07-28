# Technical Glossary

This glossary defines key technical terms used throughout the Collaborative Puzzle Platform project and the Q&A documentation.

## A

**ACID (Atomicity, Consistency, Isolation, Durability)**  
A set of properties that guarantee database transactions are processed reliably. Essential for maintaining data integrity in multi-user systems.

**API Gateway**  
A server that acts as an API front-end, receiving API requests, enforcing throttling and security policies, passing requests to the back-end service and then passing the response back to the requester.

**API Versioning**  
The practice of managing changes to an API while maintaining backward compatibility. Common strategies include URL path versioning, query string parameters, and header-based versioning.

**Application Insights**  
Microsoft Azure's application performance management (APM) service that provides real-time monitoring, alerting, and analytics for web applications.

**ASP.NET Core**  
A cross-platform, high-performance, open-source framework for building modern, cloud-enabled, Internet-connected applications.

## B

**Background Service**  
In .NET, a hosted service that runs background tasks in parallel with the main application. Implements the `IHostedService` interface.

**Backplane**  
A shared message bus that enables multiple instances of an application to communicate. SignalR uses Redis as a backplane for scaling across multiple servers.

**Bearer Token**  
An access token type that allows access to resources by presenting the token. The bearer of the token has access without further identification.

## C

**Circuit Breaker Pattern**  
A design pattern that prevents an application from repeatedly trying to execute an operation that's likely to fail, allowing it to continue without waiting for the fault to be fixed.

**Clean Architecture**  
An architectural pattern that separates concerns into layers with dependencies pointing inward, making the system more maintainable and testable.

**Connection Multiplexer**  
In StackExchange.Redis, the central object that manages connections to Redis servers, handling connection pooling, reconnection logic, and command routing.

**Content Security Policy (CSP)**  
An HTTP header that helps prevent cross-site scripting (XSS), clickjacking, and other code injection attacks by specifying which dynamic resources are allowed to load.

**Coturn**  
An open-source STUN and TURN server implementation used for NAT traversal in WebRTC applications.

## D

**Dapper**  
A lightweight Object-Relational Mapping (ORM) library for .NET that provides high performance data access with minimal overhead.

**Dependency Injection (DI)**  
A design pattern where objects receive their dependencies from external sources rather than creating them internally, improving testability and flexibility.

**Docker**  
A platform that uses OS-level virtualization to deliver software in packages called containers, which are isolated from one another and bundle their own software, libraries, and configuration files.

**DTOs (Data Transfer Objects)**  
Objects that carry data between processes to reduce the number of method calls. Used to decouple internal domain models from external API contracts.

## E

**Edge Computing**  
A distributed computing paradigm that brings computation and data storage closer to the sources of data to improve response times and save bandwidth.

**Entity Framework Core**  
Microsoft's modern object-database mapper for .NET. Supports LINQ queries, change tracking, updates, and schema migrations.

**Event Bus**  
A communication mechanism that allows different parts of a system to communicate through events without knowing about each other directly.

**Event Sourcing**  
A pattern where state changes are logged as a sequence of events rather than storing just the current state, enabling audit trails and temporal queries.

## F

**Fluent Assertions**  
A .NET library that provides a more natural way to specify expected outcomes in unit tests using a fluent API.

## G

**Graceful Degradation**  
A design principle where a system maintains limited functionality when some components fail, rather than complete system failure.

**Grafana**  
An open-source analytics and monitoring platform that integrates with various data sources including Prometheus, allowing visualization of metrics through dashboards.

**GUID (Globally Unique Identifier)**  
A 128-bit integer used to identify resources. Different versions exist for different use cases (e.g., UUID v4 for randomness, UUID v7 for time-ordering).

## H

**Health Checks**  
Endpoints that report the status of an application and its dependencies, used by load balancers and orchestrators to determine service availability.

**Horizontal Pod Autoscaling (HPA)**  
A Kubernetes feature that automatically scales the number of pods based on observed CPU utilization or other metrics.

**HSTS (HTTP Strict Transport Security)**  
A security mechanism that forces web browsers to interact with websites only over HTTPS, preventing protocol downgrade attacks.

**HTTP/2**  
The second major version of HTTP that introduced multiplexing, server push, header compression, and binary framing.

**HTTP/3**  
The third major version of HTTP that runs over QUIC instead of TCP, providing faster connection establishment and improved performance.

**HttpClientFactory**  
A factory abstraction in .NET for creating HttpClient instances, managing their lifetime to avoid socket exhaustion issues.

## I

**ICE (Interactive Connectivity Establishment)**  
A protocol used by WebRTC to find the best path for peer-to-peer communication, working with STUN and TURN servers.

**Idempotency**  
The property of an operation where multiple identical requests have the same effect as a single request. Critical for reliable distributed systems.

**Infrastructure as Code (IaC)**  
The practice of managing and provisioning infrastructure through machine-readable definition files rather than manual hardware configuration.

## J

**JWT (JSON Web Token)**  
An open standard for securely transmitting information between parties as a JSON object, commonly used for authentication and authorization.

## K

**Kestrel**  
A cross-platform web server for ASP.NET Core, designed to be fast and scalable, often used behind a reverse proxy in production.

**Kibana**  
A data visualization dashboard for Elasticsearch, commonly used for log analysis and monitoring in the ELK stack.

**KQL (Kusto Query Language)**  
A query language used in Azure services like Application Insights and Log Analytics for exploring and analyzing large volumes of data.

**Kubernetes (K8s)**  
An open-source container orchestration platform that automates the deployment, scaling, and management of containerized applications.

## L

**Lazy Loading**  
A design pattern where initialization of an object is deferred until it's actually needed, improving performance and resource usage.

**Load Balancer**  
A device or software that distributes network or application traffic across multiple servers to ensure reliability and performance.

**Lock-Free**  
Programming techniques that allow multiple threads to access shared data without using mutual exclusion locks, improving concurrency.

## M

**MessagePack**  
An efficient binary serialization format that's more compact than JSON, used for high-performance scenarios like SignalR communication.

**Microservices**  
An architectural style where applications are built as a collection of small, autonomous services that communicate through well-defined APIs.

**Minimal APIs**  
A simplified way to build HTTP APIs in ASP.NET Core with minimal dependencies and ceremony, introduced in .NET 6.

**Moq**  
A popular mocking framework for .NET used in unit testing to create mock objects and verify interactions.

**MQTT (Message Queuing Telemetry Transport)**  
A lightweight messaging protocol designed for IoT devices and low-bandwidth, high-latency networks.

## N

**NAT (Network Address Translation)**  
A method of remapping IP addresses by modifying network address information in packet headers while in transit.

**NuGet**  
The package manager for .NET that enables developers to share and consume useful code in the form of packages.

## O

**OAuth 2.0**  
An authorization framework that enables applications to obtain limited access to user accounts on an HTTP service.

**OpenTelemetry**  
A collection of tools, APIs, and SDKs for instrumenting, generating, collecting, and exporting telemetry data (metrics, logs, and traces).

**Outbox Pattern**  
A pattern for reliable message publishing where messages are first saved to a database "outbox" table before being published to ensure exactly-once delivery.

## P

**Polly**  
A .NET resilience and transient-fault-handling library that provides policies like retry, circuit breaker, timeout, and bulkhead isolation.

**Porter**  
A tool for packaging and distributing cloud-native applications using Cloud Native Application Bundles (CNAB).

**Prometheus**  
An open-source monitoring and alerting toolkit that collects and stores metrics as time series data.

**PromQL**  
Prometheus Query Language, used to query and aggregate time series data stored in Prometheus.

## Q

**QUIC**  
A transport layer protocol developed by Google that provides multiplexed connections over UDP, used by HTTP/3.

## R

**Rate Limiting**  
A technique to control the rate of requests a user can make to an API to prevent abuse and ensure fair usage.

**Redis**  
An in-memory data structure store used as a database, cache, and message broker, known for its high performance.

**Repository Pattern**  
A design pattern that encapsulates data access logic and provides a more object-oriented view of the persistence layer.

**Resilience**  
The ability of a system to handle and recover from failures gracefully without losing core functionality.

## S

**Saga Pattern**  
A pattern for managing distributed transactions where each transaction is broken into local transactions with compensating actions.

**SignalR**  
A library for ASP.NET that enables real-time web functionality, allowing server-side code to push content to clients instantly.

**Socket.IO**  
A JavaScript library for real-time web applications that enables bidirectional communication between web clients and servers.

**SQL Injection**  
A code injection technique where malicious SQL statements are inserted into application queries to manipulate or destroy data.

**SSO (Single Sign-On)**  
An authentication scheme that allows users to log in with a single ID to multiple related but independent software systems.

**STUN (Session Traversal Utilities for NAT)**  
A protocol that helps determine the public IP address and port of a device behind a NAT.

**StyleCop**  
A static code analysis tool for C# that enforces a set of style and consistency rules.

**Swagger/Swashbuckle**  
Tools for generating interactive API documentation from ASP.NET Core applications, implementing the OpenAPI specification.

## T

**TDD (Test-Driven Development)**  
A software development process where tests are written before the code that makes them pass, ensuring better design and coverage.

**Telemetry**  
Automated collection of measurements or other data at remote points and their transmission to receiving equipment for monitoring.

**Terraform**  
An infrastructure as code tool that allows you to build, change, and version infrastructure safely and efficiently.

**TTL (Time To Live)**  
A mechanism that limits the lifespan of data in a computer or network, commonly used in caching and DNS.

**TURN (Traversal Using Relays around NAT)**  
A protocol that assists in traversal of NATs or firewalls by relaying data through a server when direct connection fails.

## U

**UDP (User Datagram Protocol)**  
A connectionless transport protocol that offers faster transmission than TCP at the cost of reliability.

**Unit of Work Pattern**  
A pattern that maintains a list of business objects affected by a transaction and coordinates writing out changes.

**URI/URL**  
Uniform Resource Identifier/Locator - strings that identify and locate resources on the web.

## V

**Vertical Scaling**  
Increasing the capacity of a single server by adding more resources (CPU, RAM) rather than adding more servers.

**Virus Scanning**  
The process of detecting malicious software in uploaded files to prevent security breaches.

## W

**WebRTC (Web Real-Time Communication)**  
A free, open-source project that enables web browsers and mobile applications to conduct real-time communication via APIs.

**WebSocket**  
A protocol providing full-duplex communication channels over a single TCP connection, enabling real-time data transfer.

## X

**X-Headers**  
Custom HTTP headers typically prefixed with "X-" used to pass additional information between clients and servers.

**xUnit**  
A free, open-source, community-focused unit testing tool for the .NET Framework.

## Y

**YAML (Yet Another Markup Language)**  
A human-readable data serialization language commonly used for configuration files, particularly in Kubernetes.

## Z

**Zero-RTT (0-RTT)**  
A feature in TLS 1.3 and QUIC that allows clients to send data on the first message to the server without waiting for the handshake to complete.

**Zero Trust**  
A security model that requires strict verification for every person and device trying to access resources, regardless of location.