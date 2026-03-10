# Microservice_2 (Student Style)

Multiple ASP.NET Core microservices with **RabbitMQ** and **API Gateway** for admin and user dashboards.

## Services

| Service     | Port | Description                          |
|------------|------|--------------------------------------|
| APIGateway | 5000 | YARP reverse proxy (use this for UI) |
| User       | 8080 | User management, JWT, admin request signup |
| ADMIN      | 5087 | Request create/verify, rights        |
| Product    | 5088 | Products, auction scheduling         |
| Verify     | 5089 | Product verification by admin        |

## RabbitMQ (all services)

RabbitMQ is used across the application where required.

### Events (publisher → consumer)

- **user.created** (User) – published when a user is created; no consumer yet (for audit/analytics later).
- **request.created** (ADMIN) → **User** consumes – ADMIN publishes when a request is created; User service consumes for audit/logging.
- **product.deleted** (Product) → **Verify** consumes – removes verification record when a product is deleted.
- **product.unverified** (Verify) → **Product** consumes – clears auction dates when admin unverifies a product.

### Run RabbitMQ

```bash
docker compose up -d
```

- Management UI: `http://localhost:15672` (guest / guest)

### Run all services

1. Start RabbitMQ: `docker compose up -d`
2. In separate terminals (from repo root):

```bash
dotnet run --project APIGateway/APIGateway.csproj
dotnet run --project User/User.csproj
dotnet run --project ADMIN/ADMIN.csproj
dotnet run --project Product/Product.csproj
dotnet run --project Verify/Verify.csproj
```

Gateway runs at **http://localhost:5000**. Point your dashboard/frontend to the gateway.

## Dashboard endpoints (showcase)

Call via **Gateway** (`http://localhost:5000`) so admin and user dashboards get proper data.

### User dashboard

- `GET /api/user/dashboard` – **Authorize** – profile summary for current user.
- `GET /api/product/dashboard` – **Authorize (SELLER,USER)** – my product count.

### Admin dashboard

- `GET /api/admin/dashboard` – **Authorize (ADMIN)** – pending request count, verified-by-me list.
- `GET /api/Request/dashboard` – **AllowAnonymous** – pending and verified request counts.
- `GET /api/verify/dashboard` – **Authorize (ADMIN)** – verified count, verified-by-me count, unverified count.

Use the same JWT for user vs admin; role is in the token. CORS allows `localhost:5000`, `5087`, `8080`, `3000`.

## Notes

- Duplicate HTTP client registration in Product was removed; RabbitMQ used for product delete and unverify flows.
- Each service that publishes events has a `Messaging` folder (User, ADMIN, Product, Verify).
