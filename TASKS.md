# 📋 TASKS.md — LỘ TRÌNH XÂY DỰNG `fruit-shop-dotnet-microservices` (8 CHẶNG)

> **Quy tắc vận hành cho Antigravity IDE**:
> 1. Đọc kỹ toàn bộ `ARCHITECTURE.md` trước khi tạo file. Tuân thủ 100% phiên bản gói NuGet đã khóa cứng và mọi quy tắc kỹ thuật trong `ARCHITECTURE.md`.
> 2. Thực thi tuần tự từ **Chặng 1 → Chặng 8** không dừng hỏi xác nhận giữa chừng.
> 3. **Kỷ luật Git tiết kiệm token**: Sau mỗi chặng **1 → 7**, chỉ chạy `git add -A && git commit --no-verify -m "..."`. **KHÔNG chạy `dotnet build`, `dotnet test` hay `docker build` sau các chặng 1–7.**
> 4. Tại **Chặng 8 (Final Verification)**, chạy **một lần duy nhất** toàn bộ bước kiểm chứng (`dotnet test`, `docker compose config`, kiểm toán tĩnh) và sửa triệt để mọi lỗi cho tới khi mọi bài test đều PASS 100%.

---

## Chặng 1: Khởi tạo Solution (`.sln`), `.gitignore`, `.env.example` & `EventBus.Contracts`
- [x] Tạo `.gitignore` chuẩn .NET (`bin/`, `obj/`, `.vs/`, `.env`, `*.user`, `TestResults/`) và `.env.example` (chứa cấu hình mẫu sạch secret).
- [x] Tạo `src/BuildingBlocks/EventBus.Contracts/EventBus.Contracts.csproj` (`.NET 8`) và `OrderIntegrationEvents.cs` chứa 2 `record`: `OrderCreatedIntegrationEvent` và `OrderCancelledIntegrationEvent`.
- [x] Tạo `FruitShop.Microservices.sln` liên kết toàn bộ 6 project sẽ tạo (`EventBus.Contracts`, `ApiGateway`, `FruitCatalog.API`, `FruitBasket.API`, `FruitOrdering.API`, `FruitShop.UnitTests`) — hoặc khởi tạo dần cấu trúc `.csproj` chuẩn.
- [x] **Commit 1**: `git commit --no-verify -m "chore: initialize .NET 8 solution skeleton and EventBus.Contracts"`

---

## Chặng 2: Xây dựng `ApiGateway` (YARP + Rate Limiter + JWT Bearer + Demo Auth Issuer)
- [x] Tạo `src/Gateways/ApiGateway/ApiGateway.csproj` (YARP `2.2.0`, JwtBearer `8.0.10`, System.IdentityModel.Tokens.Jwt `8.1.2`, OpenTelemetry `1.9.0`, Swashbuckle `6.9.0`).
- [x] Tạo `Options/JwtOptions.cs` (`ValidateDataAnnotations().ValidateOnStart()`), `Exceptions/GlobalExceptionHandler.cs` (`IExceptionHandler` trả RFC 7807 `ProblemDetails`).
- [x] Tạo `Controllers/AuthController.cs` với endpoint `POST /api/v1/auth/token` phát hành JWT ký HMAC-SHA256 hợp lệ cho 3 tài khoản demo (`customer@freshfruit.vn`, `staff@freshfruit.vn`, `admin@freshfruit.vn`).
- [x] Cấu hình `appsettings.json` (YARP Routes & Clusters trỏ tới `fruit-catalog-api`, `fruit-basket-api`, `fruit-ordering-api` qua Docker DNS) và `Program.cs` (YARP, Fixed Window Rate Limiter `429`, Correlation ID middleware, JWT Auth + Authorization Policies, OpenTelemetry OTLP conditional, `/health/live` & `/health/ready`).
- [x] **Commit 2**: `git commit --no-verify -m "feat(gateway): implement YARP ApiGateway with Rate Limiter, JWT Auth and Correlation ID"`

---

## Chặng 3: Xây dựng `FruitCatalog.API` (EF Core 8 + PostgreSQL `catalog_db` + Seed Trái Cây Việt Nam)
- [x] Tạo `src/Services/FruitCatalog/FruitCatalog.API/FruitCatalog.API.csproj` (Npgsql EF Core `8.0.10`, JwtBearer `8.0.10`, HealthChecks.NpgSql `8.0.2`, OpenTelemetry `1.9.0`, Swashbuckle `6.9.0`).
- [x] Tạo `Domain/FruitProduct.cs` (Rich Entity có `[ConcurrencyCheck] int Version`, các phương thức `ReserveStock`, `ReleaseStock`, `UpdatePrice`) và `Domain/DomainExceptions.cs` (`InsufficientStockException`, `DomainException`).
- [x] Tạo `Data/CatalogDbContext.cs` (default schema `catalog`, unique index `Sku`, precision `(18,2)`) và `Data/CatalogDbInitializer.cs` (tạo schema/bảng tự động qua `EnsureCreatedAsync` + seed idempotent 6 loại trái cây đặc sản Việt Nam).
- [x] Tạo `Dtos/CatalogDtos.cs` (C# 12 `record` kèm Data Annotations validation), `Services/ICatalogService.cs` & `CatalogService.cs` (nhánh đọc dùng `.AsNoTracking().Select(...)`), `Controllers/CatalogItemsController.cs` (phân tầng C3 chuẩn, `[Authorize(Policy = "AdminOnly")]` cho `POST`, truyền đủ `CancellationToken`).
- [x] Tạo `Exceptions/GlobalExceptionHandler.cs` (`400`, `404`, `409`) và hoàn thiện `Program.cs` + `appsettings.json` (`/health/live`, `/health/ready` kiểm tra PostgreSQL, OpenTelemetry OTLP).
- [x] **Commit 3**: `git commit --no-verify -m "feat(catalog): implement FruitCatalog.API with EF Core 8, concurrency check and fruit seed data"`

---

## Chặng 4: Xây dựng `FruitBasket.API` (Redis Polyglot Store + MassTransit Idempotent Consumer)
- [x] Tạo `src/Services/FruitBasket/FruitBasket.API/FruitBasket.API.csproj` (StackExchangeRedis `8.0.10`, MassTransit.RabbitMQ `8.2.5`, JwtBearer `8.0.10`, HealthChecks.Redis `8.0.1`, OpenTelemetry `1.9.0`, Swashbuckle `6.9.0`).
- [x] Tạo `Models/CustomerBasket.cs` (`CustomerBasket`, `BasketItem`, `UpdateBasketRequest`).
- [x] Tạo `Repositories/IBasketRepository.cs`, `RedisBasketRepository.cs` (`IDistributedCache`, key `basket:{customerId}`, TTL 7 ngày) và `IProcessedEventStore.cs`, `RedisProcessedEventStore.cs` (key `processed-event:{eventId}`, TTL 24 giờ).
- [x] Tạo `Consumers/OrderCreatedConsumer.cs` (`IConsumer<OrderCreatedIntegrationEvent>`): kiểm tra `HasProcessedAsync` $\to$ xóa giỏ hàng $\to$ `MarkProcessedAsync`.
- [x] Tạo `Services/IBasketService.cs` & `BasketService.cs`, `Controllers/BasketController.cs` (`[Authorize]`, chống IDOR kiểm tra `customerId` khớp JWT `sub` hoặc role `Admin`/`Staff`), `Exceptions/GlobalExceptionHandler.cs`.
- [x] Hoàn thiện `Program.cs` + `appsettings.json` (MassTransit RabbitMQ, Redis, `/health/live` & `/health/ready` gồm Redis + MassTransit, OpenTelemetry OTLP).
- [x] **Commit 4**: `git commit --no-verify -m "feat(basket): implement FruitBasket.API with Redis store and idempotent MassTransit consumer"`

---

## Chặng 5: Xây dựng `FruitOrdering.API` — Phần 1: DDD Aggregate, EF Core `ordering_db`, CQRS Read & Outbox
- [x] Tạo `src/Services/FruitOrdering/FruitOrdering.API/FruitOrdering.API.csproj` (Npgsql EF Core `8.0.10`, `Microsoft.Extensions.Http.Resilience` `8.10.0`, `MassTransit.RabbitMQ` `8.2.5`, JwtBearer `8.0.10`, HealthChecks.NpgSql `8.0.2`, OpenTelemetry `1.9.0`, Swashbuckle `6.9.0`).
- [x] Tạo `Domain/Order.cs` (Aggregate Root với `private set`, `IReadOnlyCollection<OrderItem> Items`, Value Object `Address`, `OrderStatus` [`Submitted`, `Confirmed`, `Cancelled`], `IdempotencyKey`), `Domain/OrderItem.cs`, `Domain/IntegrationEventLogEntry.cs` (Outbox table), `Domain/OrderingExceptions.cs` (`DomainException`, `ServiceUnavailableException`, `ForbiddenAccessException`).
- [x] Tạo `Data/OrderingDbContext.cs` (default schema `ordering`, unique index `IdempotencyKey`, Owned Type `Address`) và `Data/OrderingDbInitializer.cs` (`EnsureCreatedAsync`).
- [x] Tạo `Dtos/OrderDtos.cs` (C# 12 `record` với Validation) và `Services/IOrderQueryService.cs`, `OrderQueryService.cs` (CQRS Read dùng `.AsNoTracking().Select(...)`).
- [x] Tạo `BackgroundServices/OutboxRelayBackgroundService.cs` dùng `IServiceScopeFactory.CreateScope()` (tránh Captive Dependency!) để poll `IntegrationEventLogEntry` (`NotPublished`) và publish qua MassTransit `IPublishEndpoint`.
- [x] **Commit 5**: `git commit --no-verify -m "feat(ordering): implement DDD Order aggregate, EF Core ordering_db, CQRS queries and Outbox relay"`

---

## Chặng 6: Xây dựng `FruitOrdering.API` — Phần 2: Typed `HttpClient`, Polly v8 Resilience, Saga Compensation & Controller
- [x] Tạo `Options/ServiceUrlOptions.cs` (`CatalogApi`, `BasketApi` kèm `ValidateDataAnnotations().ValidateOnStart()`).
- [x] Tạo `Clients/CatalogApiClient.cs` (`ICatalogApiClient`, Typed `HttpClient` gọi rõ ràng `/api/v1/catalog/items/{id}`, `/reserve`, `/release` — chuẩn Roslyn `CALL_SITE`!) và `Clients/BasketApiClient.cs` (`IBasketApiClient`, gọi `/api/v1/basket/{customerId}`).
- [x] Bọc bắt lỗi `BrokenCircuitException` / `TimeoutRejectedException` / `HttpRequestException` trong Typed Client $\to$ ném `ServiceUnavailableException` (`503`).
- [x] Tạo `Services/IOrderSagaService.cs` & `OrderSagaService.cs`:
  - Kiểm tra `Idempotency-Key`.
  - Lưu `Order` (`Submitted`) $\to$ giữ kho từng loại trái cây qua `ICatalogApiClient` $\to$ nếu đủ hết thì `MarkAsConfirmed()` + lưu Outbox `OrderCreatedIntegrationEvent` trong cùng `SaveChangesAsync()`.
  - Nếu lỗi giữa chừng $\to$ duyệt ngược hoàn kho qua `ReleaseStockAsync(..., CancellationToken.None)` + `order.Cancel(...)` + lưu Outbox `OrderCancelledIntegrationEvent`.
  - Hỗ trợ cả `PlaceOrderAsync` (từ payload trực tiếp) và `CheckoutFromBasketAsync` (gọi `IBasketApiClient.GetBasketAsync` lấy giỏ rồi chạy Saga).
- [x] Tạo `Controllers/OrdersController.cs` (`[Authorize]`, chống IDOR trên `GET /api/v1/orders/{id}`, đọc header `Idempotency-Key`), `Exceptions/GlobalExceptionHandler.cs` (`400`, `403`, `404`, `409`, `503`).
- [x] Hoàn thiện `Program.cs` + `appsettings.json` (đăng ký `AddHttpClient<...>().AddStandardResilienceHandler(...)` với Polly v8 Timeout/Retry/CircuitBreaker, MassTransit RabbitMQ, Outbox `AddHostedService`, `/health/live` & `/health/ready`, OpenTelemetry OTLP).
- [x] **Commit 6**: `git commit --no-verify -m "feat(ordering): add Typed HttpClients with Polly v8 resilience, Saga compensation and OrdersController"`

---

## Chặng 7: Docker Multi-Stage, `docker-compose.yml`, Unit Tests (`xUnit`) & `README.md`
- [x] Tạo Multi-Stage `Dockerfile` chuẩn `.NET 8` (`sdk:8.0-alpine` $\to$ `aspnet:8.0-alpine`, `USER $APP_UID`, `EXPOSE 8080`, `HEALTHCHECK` readiness) cho cả 4 service (`ApiGateway`, `FruitCatalog.API`, `FruitBasket.API`, `FruitOrdering.API`).
- [x] Tạo `docker-compose.yml` chuẩn MAVIS Sandbox & Studio Scale (`1 → 8`):
  - `catalog-db` (`postgres:16-alpine`), `ordering-db` (`postgres:16-alpine`), `redis` (`redis:7-alpine`), `rabbitmq` (`rabbitmq:3.13-management-alpine`) đều có `healthcheck`.
  - 4 service .NET dùng `depends_on: condition: service_healthy`, **KHÔNG `container_name:`**, chỉ phơi `ports` tại `api-gateway` (`8080:8080`) và `rabbitmq` management (`15672:15672`); 3 service nghiệp vụ chỉ dùng `expose: ["8080"]`.
- [x] Tạo project test `tests/FruitShop.UnitTests/FruitShop.UnitTests.csproj` (xUnit + NSubstitute + EF Core InMemory) và viết đủ 4 bộ test chạy độc lập không cần Docker:
  - `CatalogDomainTests.cs`: Test `FruitProduct.ReserveStock`, `ReleaseStock`, `Version` increment & `InsufficientStockException`.
  - `OrderDomainTests.cs`: Test `Order` Aggregate Root invariants (`AddItem`, `MarkAsConfirmed` trên đơn rỗng bị chặn, `Cancel`).
  - `OrderSagaServiceTests.cs`: Test `OrderSagaService` (nhánh Happy Path xác nhận + ghi Outbox; nhánh lỗi hết hàng món thứ 2 gọi bù trừ `ReleaseStockAsync` cho món 1 + chuyển `Cancelled`; nhánh trùng `Idempotency-Key` trả đơn cũ).
  - `BasketConsumerIdempotencyTests.cs`: Test `OrderCreatedConsumer` gọi xóa giỏ đúng 1 lần và bỏ qua khi nhận trùng `EventId`.
- [x] Viết `README.md` hoàn chỉnh: sơ đồ C2, hướng dẫn chạy 1 lệnh `docker compose up -d --build`, kịch bản `curl` / PowerShell end-to-end (Lấy JWT $\to$ Xem trái cây $\to$ Thêm giỏ $\to$ Đặt hàng Saga $\to$ Kiểm tra giỏ tự xóa $\to$ Demo Stop `fruit-catalog-api` & Scale `fruit-catalog-api=4`), và bảng ánh xạ 12 chương Giáo trình C# MAVIS.
- [x] **Commit 7**: `git commit --no-verify -m "feat(devops): add .NET 8 multi-stage Dockerfiles, scale-safe compose, xUnit tests and README"`

---

## Chặng 8: Kiểm chứng Tổng Lực Một Lần Duy Nhất (Final Verification)
- [x] Kiểm tra `dotnet --version` và chạy `dotnet test FruitShop.Microservices.sln --configuration Release` -> **BẮT BUỘC PASS 100% (0 build errors, 0 failed tests)**. Nếu có bất kỳ lỗi biên dịch, thiếu using, hay lỗi test nào, sửa triệt để rồi chạy lại cho đến khi xanh 100%.
- [x] Nếu `docker` có sẵn trên máy, chạy `docker compose config --quiet` để xác nhận cú pháp `docker-compose.yml` hợp lệ 100%.
- [x] Thực hiện tự kiểm toán tĩnh (Static Self-Audit):
  - Xác nhận **0 lệnh `new HttpClient(`** trong thư mục `src/`.
  - Xác nhận **0 Controller tiêm trực tiếp `DbContext`**.
  - Xác nhận **0 `container_name:`** trong `docker-compose.yml` và chỉ `api-gateway` / `rabbitmq` có `ports:`.
  - Xác nhận cả 4 `Dockerfile` đều dùng `USER $APP_UID` và có `HEALTHCHECK` `/health/ready`.
  - Xác nhận **0 secret thật** bị hard-code.
  - Cập nhật toàn bộ checkbox `[x]` trong `TASKS.md`.
- [x] Nếu có chỉnh sửa ở Chặng 8 -> **Commit 8**: `git commit --no-verify -m "fix(verify): resolve build/audit checks and finalize curated .NET lab repo"`
