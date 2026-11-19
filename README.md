# 🥭 Project Trái Cây (`fruit-shop-dotnet-microservices`)

Kho mã nguồn mẫu chuẩn hóa (**Curated Reference Repository**) cho kiến trúc Microservices trên **.NET 8.0 LTS / C# 12**, lấy miền nghiệp vụ cửa hàng trái cây sạch đặc sản Việt Nam (**FreshFruit Shop**), được tối ưu cho phân tích tĩnh **Microsoft Roslyn SDK (`MAVIS.Worker`)**, **OpenTelemetry .NET**, và thực nghiệm **Stop / Scale (1 → 8)** trên **MAVIS Studio**.

---

## 1. Kiến trúc Container (C2)

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
          Sync Typed HttpClient         |          |      |                 | Event Bus |
          + Polly v8 Resilience         |          |      |                 +-----------+
          FruitOrdering.API ────────────+──────────+      |                       |
           --> FruitCatalog.API (Get / Reserve / Release) |                       |
           --> FruitBasket.API  (GetBasket for Checkout) -+                       |
                                        ^                                         |
                                        +--- (OrderCreatedIntegrationEvent,       |
                                              Idempotent Clear Cart!) ------------+
```

---

## 2. Khởi chạy nhanh (1 lệnh Docker Compose)

```powershell
# (Tùy chọn) Sao chép file cấu hình mẫu để tùy biến mật khẩu / OTLP endpoint
Copy-Item .env.example .env

# Build và khởi chạy toàn bộ 4 Web API + PostgreSQL + Redis + RabbitMQ
docker compose up -d --build
```

- **API Gateway**: `http://localhost:8080`
- **RabbitMQ Management UI**: `http://localhost:15672` (`guest` / `guest`)
- **Health Checks** (trên mỗi dịch vụ): `/health/live` (liveness) và `/health/ready` (readiness với PostgreSQL / Redis / MassTransit RabbitMQ).

---

## 3. Kịch bản kiểm thử End-to-End (PowerShell)

### Bước 1: Lấy JWT từ Demo Auth Issuer
```powershell
$loginBody = @{
    email = "customer@freshfruit.vn"
    password = "FreshFruit@123"
} | ConvertTo-Json

$auth = Invoke-RestMethod -Method Post `
    -Uri "http://localhost:8080/api/v1/auth/token" `
    -ContentType "application/json" `
    -Body $loginBody

$headers = @{ Authorization = "Bearer $($auth.accessToken)" }
```

### Bước 2: Xem danh sách trái cây đặc sản Việt Nam (Public Read)
```powershell
Invoke-RestMethod -Method Get -Uri "http://localhost:8080/api/v1/catalog/items"
```

### Bước 3: Thêm giỏ hàng vào Redis
```powershell
$basketBody = @{
    items = @(
        @{ productId = 1; productName = "Xoài Cát Hòa Lộc Loại 1"; unitPrice = 95000; quantity = 2; unit = "kg" },
        @{ productId = 3; productName = "Bưởi Da Xanh Bến Tre"; unitPrice = 65000; quantity = 1; unit = "trái" }
    )
} | ConvertTo-Json -Depth 5

Invoke-RestMethod -Method Put `
    -Uri "http://localhost:8080/api/v1/basket/customer-01" `
    -Headers $headers `
    -ContentType "application/json" `
    -Body $basketBody
```

### Bước 4: Đặt hàng từ giỏ (Saga + Outbox + Auto Clear Basket)
```powershell
$orderHeaders = @{
    Authorization   = "Bearer $($auth.accessToken)"
    "Idempotency-Key" = "order-$([guid]::NewGuid().ToString('N'))"
}

$checkoutBody = @{
    shippingAddress = @{
        street = "45 Nguyễn Huệ"
        ward   = "Bến Nghé"
        city   = "TP. Hồ Chí Minh"
    }
} | ConvertTo-Json

Invoke-RestMethod -Method Post `
    -Uri "http://localhost:8080/api/v1/orders/checkout-basket" `
    -Headers $orderHeaders `
    -ContentType "application/json" `
    -Body $checkoutBody
```

### Bước 5: Xác nhận giỏ hàng đã được dọn sạch bất đồng bộ qua RabbitMQ
```powershell
Start-Sleep -Seconds 3
Invoke-RestMethod -Method Get -Uri "http://localhost:8080/api/v1/basket/customer-01" -Headers $headers
```

### Bước 6: Thực nghiệm MAVIS Studio (Chaos Stop & Horizontal Scale)
```powershell
# 1. Stop FruitCatalog.API -> Polly v8 Retry + Circuit Breaker -> Fail-Fast 503 ProblemDetails
docker compose stop fruit-catalog-api

# 2. Scale FruitCatalog.API từ 1 -> 4 replica (Docker Embedded DNS Round-Robin, 0 xung đột cổng)
docker compose start fruit-catalog-api
docker compose up -d --scale fruit-catalog-api=4
```

---

## 4. Chạy Unit Tests không cần Docker

```powershell
dotnet test FruitShop.Microservices.sln --configuration Release
```

---

## 5. Ánh xạ 12 Chương Giáo trình C# / .NET 8 MAVIS

| Chương | Chủ đề cốt lõi | Thành phần hiện thực trong Repo |
|---|---|---|
| **Ch. 1–2** | Bounded Context & Clean C3 Layering | Tách 3 context độc lập (`Catalog`, `Basket`, `Ordering`), chỉ chia sẻ `EventBus.Contracts`; `Controller -> Service -> DbContext` |
| **Ch. 3** | API Gateway & Rate Limiting | `ApiGateway` dùng `Yarp.ReverseProxy` + `AddFixedWindowLimiter` (`429 Too Many Requests`) + `X-Correlation-Id` |
| **Ch. 4** | EF Core 8 & Optimistic Concurrency | `FruitCatalog.API` dùng `Npgsql`, `EnsureCreatedAsync()`, `[ConcurrencyCheck] int Version` |
| **Ch. 5** | Polyglot Persistence (Redis) | `FruitBasket.API` dùng `IDistributedCache` (TTL 7 ngày) tách biệt hoàn toàn khỏi PostgreSQL |
| **Ch. 6** | DDD Aggregate & CQRS | `Order` Aggregate Root (`private set`, invariants), `Address` Value Object, `OrderQueryService` (`.AsNoTracking().Select(...)`) |
| **Ch. 7** | Security & Anti-IDOR | JWT HMAC-SHA256, Policy RBAC (`AuthenticatedUser`, `AdminOnly`), kiểm tra sở hữu `sub` trên Basket & Ordering |
| **Ch. 8** | Typed `HttpClient` & Polly v8 | `CatalogApiClient` & `BasketApiClient` gắn `AddStandardResilienceHandler` (Timeout, Retry + Jitter, Circuit Breaker) |
| **Ch. 9** | Orchestration Saga & Compensation | `OrderSagaService`: `Submitted` (Semantic Lock) → `ReserveStock` → `Confirmed`, lỗi giữa chừng hoàn kho ngược (`ReleaseStockAsync`) |
| **Ch. 10** | Transactional Outbox & Idempotency | `IntegrationEventLogEntry` cùng transaction với `Order`; `OutboxRelayBackgroundService` dùng `IServiceScopeFactory`; `RedisProcessedEventStore` |
| **Ch. 11** | Health Checks & OpenTelemetry | `/health/live`, `/health/ready` (`NpgSql`, `Redis`, `masstransit-bus`), OTel Traces + Metrics (lọc `/health`) |
| **Ch. 12** | Docker Multi-Stage & Scale (1 → 8) | 4 `Dockerfile` Alpine non-root (`USER $APP_UID`), `docker-compose.yml` không `container_name:`, chỉ `expose: ["8080"]` cho 3 service |
