# 🏛️ ARCHITECTURE.md — PROJECT TRÁI CÂY MICROSERVICES (`fruit-shop-dotnet-microservices`)

> **Tên kho mã nguồn mới**: `fruit-shop-dotnet-microservices`
> **Vai trò**: Kho mã nguồn mẫu chuẩn hóa (Curated Reference Repository) cho khóa học **C# 12 / .NET 8 & ASP.NET Core Microservices (`CSHARP-MS-V2`)** trên nền tảng **MAVIS**.
> **Miền nghiệp vụ (Domain)**: Cửa hàng trái cây sạch đặc sản Việt Nam **Project Trái Cây (FreshFruit Shop)** (*Xoài Cát Hòa Lộc, Sầu Riêng Ri6, Bưởi Da Xanh Bến Tre, Thanh Long Ruột Đỏ, Cam Sành Vĩnh Long, Mãng Cầu Xiêm*).
> **Mục tiêu chất lượng**: Đạt trọn vẹn **10/10 tiêu chí** ở Phụ lục C Giáo trình C# 12 Chương, tối ưu cho bộ phân tích tĩnh **Microsoft Roslyn SDK (`MAVIS.Worker`)** (`C1–C3`, `3E Rules`, `CALL_SITE`, `Reverse BFS`), chạy mượt **Docker Runtime + OpenTelemetry .NET** và sẵn sàng thực nghiệm **Stop / Scale ($1 \to 8$)** trên **MAVIS Studio**.

---

## 1. STACK CÔNG NGHỆ & PHIÊN BẢN NUGET CHỐT CỨNG (LOCKED VERSIONS)

Tất cả các dự án đều target **`.NET 8.0 LTS` (`<TargetFramework>net8.0</TargetFramework>`)**, bật `<Nullable>enable</Nullable>` và `<ImplicitUsings>enable</ImplicitUsings>`, dùng chung quản lý phiên bản rõ ràng để tránh mọi xung đột gói NuGet:

| Thành phần | Công nghệ / Gói NuGet chốt cứng | Phiên bản | Lý do kỹ thuật & Lưu ý sống còn |
|---|---|---|---|
| **Runtime & Ngôn ngữ** | **.NET 8.0 LTS** / **C# 12** | `8.0` | Primary Constructors, `record` DTO, `IExceptionHandler`, non-root image `USER $APP_UID`. |
| **API Gateway** | `Yarp.ReverseProxy` | `2.2.0` | Chuẩn chính chủ Microsoft thay thế Ocelot; kết hợp ASP.NET Core 8 built-in `AddRateLimiter`. |
| **ORM & Database** | `Npgsql.EntityFrameworkCore.PostgreSQL` + **PostgreSQL `16-alpine`** | `8.0.10` | Database-per-Service (`catalog_db`, `ordering_db`); dùng `[ConcurrencyCheck] int Version` (tương thích 100% cả PostgreSQL lẫn EF Core InMemory test, tránh lỗi `byte[]` RowVersion của Npgsql). |
| **Cache & Basket Store** | `Microsoft.Extensions.Caching.StackExchangeRedis` + **Redis `7-alpine`** | `8.0.10` | Polyglot Persistence cho `FruitBasket.API` (`IDistributedCache` + TTL 7 ngày) và kho kiểm tra `Idempotency` sự kiện. |
| **Event Bus** | `MassTransit.RabbitMQ` + **RabbitMQ `3.13-management-alpine`** | `8.2.5` | Giao tiếp bất đồng bộ chuẩn .NET; tự động đăng ký readiness healthcheck `"masstransit-bus"` (tránh xung đột phiên bản với `RabbitMQ.Client` v7). |
| **HTTP Resilience** | `Microsoft.Extensions.Http.Resilience` (Polly v8) | `8.10.0` | `AddStandardResilienceHandler()` (AttemptTimeout, Retry + Exponential Backoff + Jitter, Circuit Breaker, TotalRequestTimeout). |
| **Security (JWT)** | `Microsoft.AspNetCore.Authentication.JwtBearer` + `System.IdentityModel.Tokens.Jwt` | `8.0.10` / `8.1.2` | Stateless JWT HMAC-SHA256, Policy-based RBAC (`AuthenticatedUser`, `AdminOnly`), chống IDOR. |
| **Health Checks** | `AspNetCore.HealthChecks.NpgSql` + `AspNetCore.HealthChecks.Redis` | `8.0.2` / `8.0.1` | Tách bạch `/health/live` (self) và `/health/ready` (DB / Redis / MassTransit RabbitMQ). |
| **Observability** | `OpenTelemetry.Extensions.Hosting`, `.Instrumentation.AspNetCore`, `.Instrumentation.Http`, `.Instrumentation.Runtime`, `.Exporter.OpenTelemetryProtocol` | `1.9.0` | Tự động truyền W3C `traceparent`; chỉ kích hoạt OTLP exporter khi biến `OTEL_EXPORTER_OTLP_ENDPOINT` không rỗng. |
| **API Docs & Test** | `Swashbuckle.AspNetCore` (`6.9.0`), `xunit` (`2.9.2`), `xunit.runner.visualstudio` (`2.8.2`), `Microsoft.NET.Test.Sdk` (`17.11.1`), `NSubstitute` (`5.3.0`), `Microsoft.EntityFrameworkCore.InMemory` (`8.0.10`) | Locked | Swagger UI sạch cho kiểm thử; xUnit chạy cực nhanh không cần Docker thật. |

### 🚫 Quy tắc CẤM tuyệt đối (Hard Constraints)
- **KHÔNG** tạo `SharedDomain.csproj` hay chia sẻ bất kỳ Entity / `DbContext` nào giữa các Microservice. Chỉ chia sẻ duy nhất `EventBus.Contracts.csproj` (chứa 2 `record` Integration Event siêu gọn).
- **KHÔNG** dùng `new HttpClient()` thủ công trong bất kỳ lớp nào — mọi lời gọi HTTP nội bộ bắt buộc đi qua `IHttpClientFactory` + **Typed `HttpClient`**.
- **KHÔNG** tiêm `DbContext` trực tiếp vào Controller (luôn tuân thủ phân tầng C3 `Controller -> Service -> DbContext/Repository`).
- **KHÔNG** để xảy ra lỗi **Captive Dependency** (tiêm `Scoped` service như `DbContext` vào `Singleton` hay `BackgroundService` — trong `BackgroundService` bắt buộc dùng `IServiceScopeFactory.CreateScope()`).
- **KHÔNG** phụ thuộc bất kỳ dịch vụ bên thứ 3 nào (VNPay, Cloud DB, SMTP thật, S3) và **KHÔNG** hard-code secret.

---

## 2. KIẾN TRÚC TỔNG THỂ (C1–C3) & TOPOLOGY TỐI ƯU CHO ROSLYN + REVERSE BFS

### 2.1. Sơ đồ Container (C2)

```text
                       [Customer / Staff / Admin]
                                   |
                         HTTP / HTTPS (Port 8080)
                                   v
                 +------------------------------------+
                 |         ApiGateway (.NET 8)        |
                 |  YARP Reverse Proxy + Rate Limiter |
                 |  JWT Auth + Demo Token Issuer      |
                 +------------------------------------+
                    /              |                \
    (Docker DNS)   /               |                 \
                  v                v                  v
     +------------------+  +------------------+  +------------------------+
     | FruitCatalog.API |  | FruitBasket.API  |  |   FruitOrdering.API    |
     |   (expose 8080)  |  |  (expose 8080)   |  |     (expose 8080)      |
     +------------------+  +------------------+  +------------------------+
              |                 |       ^          |      |      |       \
              v                 v       |          |      |      |        \ (Outbox -> Async)
       [(catalog_db)]       [(Redis)]   |          |      |      v         v
        PostgreSQL 16        Cache/TTL  |          |      | [(ordering_db)] +-----------+
                                        |          |      |  PostgreSQL 16  | RabbitMQ  |
          Sync Typed HttpClient (CALL_SITE!)       |      |                 | Event Bus |
          + Polly v8 StandardResilienceHandler     |      |                 +-----------+
          FruitOrdering.API ───────────────────────+      |                       |
           --> FruitCatalog.API (Get / Reserve / Release) |                       |
           --> FruitBasket.API  (GetBasket for Checkout) -+                       |
                                        ^                                         |
                                        +--- (Consume OrderCreatedIntegrationEvent,
                                              Idempotent Clear Cart!) ------------+
```

### 2.2. Giá trị kiểm chứng trên MAVIS (Roslyn Static, 3E Rules, Reverse BFS, Studio)
- **Database-per-Service & Polyglot Persistence (`0 SHARED_DATABASE`)**:
  - `FruitCatalog.API` $\to$ `catalog_db` (PostgreSQL, schema `catalog`)
  - `FruitBasket.API` $\to$ `redis` (Redis, key prefix `basket:` và `processed-event:`)
  - `FruitOrdering.API` $\to$ `ordering_db` (PostgreSQL, schema `ordering`)
- **Đồ thị Sync HTTP thuần DAG (`0 Cyclic Dependencies`) & Reverse BFS chuẩn mực**:
  - Cạnh xuôi HTTP (có đủ bằng chứng Roslyn `CALL_SITE` từ YARP config và Typed `HttpClient`):
    - `ApiGateway -> FruitCatalog.API`
    - `ApiGateway -> FruitBasket.API`
    - `ApiGateway -> FruitOrdering.API`
    - `FruitOrdering.API -> FruitCatalog.API` (`CatalogApiClient.cs`)
    - `FruitOrdering.API -> FruitBasket.API` (`BasketApiClient.cs`)
  - Khi chạy **Reverse BFS** trên `FruitCatalog.API`:
    - `Direct Callers (hop = 1)`: `FruitOrdering.API`, `ApiGateway`.
  - Khi chạy **MAVIS Studio Experiments**:
    1. **Stop `FruitCatalog.API`**: Gọi `POST /api/v1/orders` $\to$ `FruitOrdering.API` kích hoạt Polly v8 Retry $\to$ Circuit Breaker `OPEN` (`BrokenCircuitException`) $\to$ Fail-Fast trả `503 Service Unavailable` (`ProblemDetails`) thay vì treo luồng!
    2. **Stop `FruitBasket.API`**: Gọi `POST /api/v1/orders` (truyền danh sách trái cây trực tiếp trong body) $\to$ **Đơn hàng vẫn tạo thành công `201 Created`**, sự kiện `OrderCreatedIntegrationEvent` nằm an toàn trong hàng đợi RabbitMQ; khi **Start** lại `FruitBasket.API`, Consumer tự động tiêu thụ bù và dọn sạch giỏ hàng!
    3. **Scale `fruit-catalog-api` ($1 \to 4$)**: Docker Embedded DNS tự động cân bằng tải Round-Robin giữa 4 replica mà không cần sửa code hay cấu hình cổng!

---

## 3. CẤU TRÚC SOLUTION & CHI TIẾT TỪNG PROJECT

### 3.1. Cấu trúc thư mục Root

```text
fruit-shop-dotnet-microservices/
├── FruitShop.Microservices.sln
├── docker-compose.yml
├── .env.example
├── .gitignore
├── README.md
├── ARCHITECTURE.md
├── TASKS.md
├── src/
│   ├── BuildingBlocks/
│   │   └── EventBus.Contracts/
│   │       ├── EventBus.Contracts.csproj
│   │       └── OrderIntegrationEvents.cs
│   ├── Gateways/
│   │   └── ApiGateway/
│   │       ├── ApiGateway.csproj
│   │       ├── Dockerfile
│   │       ├── Program.cs
│   │       ├── appsettings.json
│   │       ├── Options/JwtOptions.cs
│   │       ├── Controllers/AuthController.cs
│   │       └── Exceptions/GlobalExceptionHandler.cs
│   └── Services/
│       ├── FruitCatalog/
│       │   └── FruitCatalog.API/
│       │       ├── FruitCatalog.API.csproj
│       │       ├── Dockerfile
│       │       ├── Program.cs
│       │       ├── appsettings.json
│       │       ├── Domain/
│       │       ├── Data/
│       │       ├── Dtos/
│       │       ├── Services/
│       │       ├── Controllers/
│       │       ├── Options/
│       │       └── Exceptions/
│       ├── FruitBasket/
│       │   └── FruitBasket.API/
│       │       ├── FruitBasket.API.csproj
│       │       ├── Dockerfile
│       │       ├── Program.cs
│       │       ├── appsettings.json
│       │       ├── Models/
│       │       ├── Repositories/
│       │       ├── Services/
│       │       ├── Consumers/
│       │       ├── Controllers/
│       │       ├── Options/
│       │       └── Exceptions/
│       └── FruitOrdering/
│           └── FruitOrdering.API/
│               ├── FruitOrdering.API.csproj
│               ├── Dockerfile
│               ├── Program.cs
│               ├── appsettings.json
│               ├── Domain/
│               ├── Data/
│               ├── Dtos/
│               ├── Clients/
│               ├── Services/
│               ├── BackgroundServices/
│               ├── Controllers/
│               ├── Options/
│               └── Exceptions/
└── tests/
    └── FruitShop.UnitTests/
        ├── FruitShop.UnitTests.csproj
        ├── CatalogDomainTests.cs
        ├── OrderDomainTests.cs
        ├── OrderSagaServiceTests.cs
        └── BasketConsumerIdempotencyTests.cs
```

---

### 3.2. `EventBus.Contracts` (Class Library — `.NET 8`)
Chỉ chứa 2 `record` Integration Events bất biến (có đủ `EventId`, `OccurredAtUtc`, `CorrelationId`):
- `OrderCreatedIntegrationEvent(Guid EventId, DateTime OccurredAtUtc, string CorrelationId, Guid OrderId, string CustomerId, decimal TotalAmount)`
- `OrderCancelledIntegrationEvent(Guid EventId, DateTime OccurredAtUtc, string CorrelationId, Guid OrderId, string CustomerId, string Reason)`

---

### 3.3. `ApiGateway` (YARP + Rate Limiter + JWT Bearer + Demo Auth Issuer)
- **YARP `ReverseProxy` Routes (`appsettings.json`)**:
  - `catalog-route`: `/api/v1/catalog/{**catch-all}` $\to$ `http://fruit-catalog-api:8080` (Public read; endpoint ghi được bảo vệ `AdminOnly` tại `FruitCatalog.API`).
  - `basket-route`: `/api/v1/basket/{**catch-all}` $\to$ `http://fruit-basket-api:8080` (`AuthorizationPolicy: "AuthenticatedUser"`).
  - `ordering-route`: `/api/v1/orders/{**catch-all}` $\to$ `http://fruit-ordering-api:8080` (`AuthorizationPolicy: "AuthenticatedUser"`).
- **Rate Limiter**: `AddFixedWindowLimiter("gateway-fixed", opt => { Window = 1 min; PermitLimit = 120; })`, rejection code `429 Too Many Requests`.
- **Correlation ID Middleware**: Đọc hoặc sinh mới `X-Correlation-Id` (UUID) gắn vào request & response.
- **Demo JWT Issuer (`AuthController`)**:
  - `POST /api/v1/auth/token`: Nhận `DemoLoginRequest(string Email, string Password)` cho 3 tài khoản lab gọn nhẹ (`customer@freshfruit.vn` / `Customer`, `staff@freshfruit.vn` / `Staff`, `admin@freshfruit.vn` / `Admin`, mật khẩu demo đọc từ config/env hoặc `FreshFruit@123` chỉ dành cho lab) $\rightarrow$ ký JWT chuẩn HMAC-SHA256 dùng chung `JwtOptions` (`Issuer`, `Audience`, `SigningKey`) với 3 Microservice phía sau!
  - *(Thiết kế này giữ Solution siêu gọn đúng 4 project Web API như Phụ lục C mà vẫn chạy JWT Bearer + RBAC thật 100% end-to-end!)*
- **Health Checks**: `/health/live` và `/health/ready`.

---

### 3.4. `FruitCatalog.API` (CRUD Data-Driven Service + EF Core 8 + PostgreSQL `catalog_db`)
- **Domain Entity `FruitProduct`**:
  - `Id` (`int PK`), `Sku` (`string UNIQUE`), `Name`, `Category`, `Origin` (*Cái Bè - Tiền Giang*, *Bến Tre*, *Bình Thuận*...), `Unit` (`"kg"`, `"trái"`), `UnitPrice` (`decimal(18,2)`), `AvailableStock` (`int`), `Version` (`int`, `[ConcurrencyCheck]`), `CreatedAtUtc`.
  - Phương thức miền (không Anemic): `ReserveStock(int quantity)` (ném `InsufficientStockException` nếu thiếu, trừ kho và `Version++`), `ReleaseStock(int quantity)` (cộng hoàn kho và `Version++`), `UpdatePrice(decimal newPrice)`.
- **Database Initialization (`CatalogDbInitializer`)**:
  - Dùng `EnsureCreatedAsync()` (tạo schema/tables tự động sạch sẽ ngay lần khởi chạy đầu tiên, không rủi ro lỗi snapshot migration) + idempotent seed 6 loại trái cây đặc sản Việt Nam:
    1. `FR-XOAI-01` — Xoài Cát Hòa Lộc Loại 1 (`95000`, `kg`, tồn `80`)
    2. `FR-SAURIENG-01` — Sầu Riêng Ri6 Cơm Vàng (`165000`, `kg`, tồn `45`)
    3. `FR-BUOI-01` — Bưởi Da Xanh Bến Tre (`65000`, `trái`, tồn `100`)
    4. `FR-THANHLONG-01` — Thanh Long Ruột Đỏ Bình Thuận (`42000`, `kg`, tồn `120`)
    5. `FR-CAM-01` — Cam Sành Vĩnh Long (`35000`, `kg`, tồn `150`)
    6. `FR-MANCAU-01` — Mãng Cầu Xiêm Tươi (`58000`, `kg`, tồn `60`)
- **Phân tầng C3 sạch**: `CatalogItemsController` $\to$ `ICatalogService` (`CatalogService`) $\to$ `CatalogDbContext` (nhánh đọc dùng `.AsNoTracking().Select(...)`).
- **Endpoints**:
  - `GET /api/v1/catalog/items` (lọc theo `category`, `search`, phân trang)
  - `GET /api/v1/catalog/items/{id:int}` (`200` / `404`)
  - `POST /api/v1/catalog/items` (`[Authorize(Policy = "AdminOnly")]` $\to$ `201 Created`)
  - `POST /api/v1/catalog/items/{id:int}/reserve` (`StockAdjustmentRequest(int Quantity)` $\to$ `200 OK` / `409 Conflict`)
  - `POST /api/v1/catalog/items/{id:int}/release` (`StockAdjustmentRequest(int Quantity)` $\to$ `200 OK` — dùng cho bù trừ Saga)
- **Health Checks**: `/health/live` (`self`) và `/health/ready` (`postgres`).

---

### 3.5. `FruitBasket.API` (Polyglot Redis Service + MassTransit Idempotent Consumer)
- **Models**: `CustomerBasket(string CustomerId, List<BasketItem> Items)`, `BasketItem(int ProductId, string ProductName, decimal UnitPrice, int Quantity, string Unit)`.
- **Repository (`RedisBasketRepository`)**: Dùng `IDistributedCache` lưu JSON tại key `basket:{customerId}` với `AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7)`.
- **Idempotency Store (`RedisProcessedEventStore`)**: Lưu key `processed-event:{eventId}` với TTL 24 giờ để chống xử lý trùng lặp.
- **Kafka/RabbitMQ Consumer (`OrderCreatedConsumer : IConsumer<OrderCreatedIntegrationEvent>`)**:
  - Kiểm tra `HasProcessedAsync(evt.EventId)` $\to$ nếu trùng thì log & bỏ qua; ngược lại gọi `DeleteBasketAsync(evt.CustomerId)` và `MarkProcessedAsync(evt.EventId)`.
- **Phân tầng C3 sạch**: `BasketController` (`[Authorize]`, kiểm tra `customerId == currentUserId` chống IDOR) $\to$ `IBasketService` $\to$ `IBasketRepository`.
- **Endpoints**:
  - `GET /api/v1/basket/{customerId}`
  - `PUT /api/v1/basket/{customerId}` (`UpdateBasketRequest`)
  - `DELETE /api/v1/basket/{customerId}` (`204 No Content`)
- **Health Checks**: `/health/live` (`self`) và `/health/ready` (`redis` + MassTransit built-in `ready` tag).

---

### 3.6. `FruitOrdering.API` (DDD Aggregate + CQRS + Typed `HttpClient` + Polly v8 + Saga + Outbox)
Trái tim của nhánh C# để chứng minh trọn bộ kiến thức Chương 2, 6, 8, 9, 10, 11:
- **DDD Aggregate (`Domain/`)**:
  - `Address` (`sealed record Value Object`: `Street`, `Ward`, `City`).
  - `Order` (Aggregate Root, `private set`, `IReadOnlyCollection<OrderItem> Items`, `OrderStatus`: `Submitted` [Semantic Lock], `Confirmed`, `Cancelled`, `IdempotencyKey` UNIQUE).
  - `OrderItem` (lưu `ProductId`, `ProductNameSnapshot`, `UnitPriceSnapshot`, `Quantity`).
  - `IntegrationEventLogEntry` (Outbox table: `EventId Guid PK`, `EventType string`, `PayloadJson string`, `State OutboxEventState [NotPublished, Published]`, `CreatedAtUtc`, `PublishedAtUtc`).
- **Typed `HttpClient` + Polly v8 Resilience (`Clients/`)**:
  - `ICatalogApiClient` / `CatalogApiClient` gọi `FruitCatalog.API`:
    - `GetItemAsync(int productId, CancellationToken ct)` $\to$ `GET /api/v1/catalog/items/{productId}`
    - `ReserveStockAsync(int productId, int quantity, CancellationToken ct)` $\to$ `POST /api/v1/catalog/items/{productId}/reserve`
    - `ReleaseStockAsync(int productId, int quantity, CancellationToken ct)` $\to$ `POST /api/v1/catalog/items/{productId}/release`
  - `IBasketApiClient` / `BasketApiClient` gọi `FruitBasket.API`:
    - `GetBasketAsync(string customerId, string? bearerToken, CancellationToken ct)` $\to$ `GET /api/v1/basket/{customerId}`
  - Cả hai đăng ký bằng `AddHttpClient<...>(...).AddStandardResilienceHandler(...)` với:
    - `AttemptTimeout`: `3s`
    - `Retry`: `MaxRetryAttempts = 2`, `Delay = 300ms`, `Exponential`, `UseJitter = true`
    - `CircuitBreaker`: `SamplingDuration = 30s`, `MinimumThroughput = 5`, `FailureRatio = 0.5`, `BreakDuration = 15s`
    - `TotalRequestTimeout`: `8s`
  - Bắt `BrokenCircuitException`, `TimeoutRejectedException`, `HttpRequestException` $\to$ ném `ServiceUnavailableException` (`HTTP 503 ProblemDetails`).
- **Orchestration Saga + Outbox (`OrderSagaService`)**:
  1. Kiểm tra `Idempotency-Key` (nếu đã có trong `orders`, trả ngay `OrderResponse` cũ).
  2. Gọi `ICatalogApiClient.GetItemAsync` xác nhận thông tin trái cây & giá snapshot.
  3. **T1 (Semantic Lock)**: Tạo `Order.CreateDraft(...)` (`Submitted`) và `SaveChangesAsync`.
  4. **T2 (Reserve Stock)**: Duyệt từng mặt hàng gọi `ICatalogApiClient.ReserveStockAsync`, lưu danh sách đã giữ.
  5. **T3 (Pivot + Transactional Outbox)**: `order.MarkAsConfirmed()`, đồng thời thêm `IntegrationEventLogEntry` (`OrderCreatedIntegrationEvent`, `NotPublished`) trong **cùng một `SaveChangesAsync()`** (nguyên tử trên `ordering_db`!).
  6. **Compensation Path (Nếu bước T2 hết hàng hoặc lỗi mạng/Circuit Open)**:
     - Duyệt ngược danh sách đã giữ, gọi `ICatalogApiClient.ReleaseStockAsync(..., CancellationToken.None)` hoàn kho.
     - `order.Cancel(reason)` + thêm `IntegrationEventLogEntry` (`OrderCancelledIntegrationEvent`, `NotPublished`) và `SaveChangesAsync(CancellationToken.None)`.
     - Ném lại lỗi nghiệp vụ (`409 Conflict` hoặc `503 Service Unavailable`).
- **Outbox Relay (`OutboxRelayBackgroundService : BackgroundService`)**:
  - Dùng `IServiceScopeFactory.CreateScope()` mỗi chu kỳ 2 giây (chống **Captive Dependency**!) để lấy `OrderingDbContext` + `IPublishEndpoint` (MassTransit), đọc tối đa 50 bản ghi `NotPublished`, publish lên RabbitMQ rồi đánh dấu `Published`.
- **CQRS Read Service (`OrderQueryService`)**: Truy vấn đọc dùng `.AsNoTracking().Select(...)` chiếu thẳng sang `OrderSummaryDto` / `OrderResponse`.
- **Endpoints (`OrdersController` — `[Authorize]`)**:
  - `POST /api/v1/orders` (nhận header `Idempotency-Key` + `CreateOrderRequest` $\to$ `201 Created`).
  - `POST /api/v1/orders/checkout-basket` (gọi `IBasketApiClient.GetBasketAsync` lấy giỏ hàng rồi chạy cùng `OrderSagaService` $\to$ `201 Created`).
  - `GET /api/v1/orders/{id:guid}` (chống IDOR: chỉ chủ đơn hàng hoặc `Admin`/`Staff` được xem, sai trả `403 Forbidden`).
  - `GET /api/v1/orders/my` (lấy danh sách đơn hàng của user hiện tại).
- **Health Checks**: `/health/live` (`self`) và `/health/ready` (`postgres` + MassTransit built-in `ready` tag).

---

## 4. DOCKER MULTI-STAGE, SCALE-SAFE (`1 → 8`) & OPENTELEMETRY

### 4.1. Multi-Stage `Dockerfile` chuẩn .NET 8 cho cả 4 Web API
- Stage 1 (`build`): `mcr.microsoft.com/dotnet/sdk:8.0-alpine` — `COPY` các file `.csproj` trước để tận dụng Docker Layer Cache $\to$ `dotnet restore` $\to$ `COPY . .` $\to$ `dotnet publish -c Release -o /app/publish /p:UseAppHost=false`.
- Stage 2 (`final`): `mcr.microsoft.com/dotnet/aspnet:8.0-alpine` — `USER $APP_UID` (non-root mặc định .NET 8), `EXPOSE 8080`, `HEALTHCHECK` dùng `wget -qO- http://localhost:8080/health/ready || exit 1`.

### 4.2. Quy tắc vàng trong `docker-compose.yml` cho MAVIS Sandbox & Studio
1. **KHÔNG dùng `container_name:`** trên bất kỳ service nào.
2. **Chỉ phơi cổng host ở biên**:
   - `api-gateway`: `ports: ["8080:8080"]`
   - `rabbitmq`: `ports: ["15672:15672"]` (tiện xem RabbitMQ Management UI khi demo)
   - 3 dịch vụ nghiệp vụ (`fruit-catalog-api`, `fruit-basket-api`, `fruit-ordering-api`): **CHỈ dùng `expose: ["8080"]`**, KHÔNG gán `ports:` cố định để lệnh `docker compose up -d --scale fruit-catalog-api=4` chạy mượt không xung đột cổng!
3. **Healthchecks + `depends_on: condition: service_healthy`** cho `catalog-db`, `ordering-db`, `redis`, `rabbitmq` và các dịch vụ phụ thuộc.
4. **Cấu hình Externalized (`__`) & OpenTelemetry**: Truyền `ConnectionStrings__*`, `ServiceUrls__*`, `Jwt__*`, `RabbitMq__*`, `OTEL_EXPORTER_OTLP_ENDPOINT=${OTEL_EXPORTER_OTLP_ENDPOINT:-}` qua biến môi trường.