# Complete Microservices Docker Setup Guide

## 📋 Overview

This guide covers the complete dockerization of all 6 microservices:
- **Admin Service** (Port 5087)
- **User Service** (Port 8080)
- **Product Service** (Port 5088)
- **Verify Service** (Port 5089)
- **Auction Service** (Port 5001) - with SignalR & Redis
- **API Gateway** (Port 5000)

Plus infrastructure:
- SQL Server 2022 (Port 1433)
- RabbitMQ 3 (Port 5672, Admin: 15672)
- Redis 7 (Port 6379)

---

## 🚀 Quick Start

### Option 1: Automated Setup (Recommended)
```bash
cd /home/harshid/Project_Practice/_net/Microservice_2/tuesday
chmod +x docker-startup.sh
./docker-startup.sh
```

### Option 2: Manual Setup
```bash
# Build all Docker images
docker-compose build

# Start all services
docker-compose up -d

# Monitor startup progress
docker-compose logs -f
```

---

## 📊 Services Summary

| Service | Port | Database | Key Features | Status |
|---------|------|----------|--------------|--------|
| **Admin** | 5087 | AdminDb | Request management, JWT Auth | ✓ Dockerized |
| **User** | 8080 | UserDb | User authentication, Cloudinary | ✓ Dockerized |
| **Product** | 5088 | ProductDb | Product management, Verification | ✓ Dockerized |
| **Verify** | 5089 | VerifyDb | Product verification | ✓ Dockerized |
| **Auction** | 5001 | AuctionDb | SignalR, Redis cache, Real-time bidding | ✓ Dockerized |
| **API Gateway** | 5000 | - | YARP reverse proxy | ✓ Dockerized |

---

## 🔌 Access Points

### From Your Machine (localhost)

**Microservices:**
- Admin Service: `http://localhost:5087`
- User Service: `http://localhost:8080`
- Product Service: `http://localhost:5088`
- Verify Service: `http://localhost:5089`
- Auction Service: `http://localhost:5001`
- API Gateway: `http://localhost:5000`

**Real-time Communication:**
- Auction SignalR Hub: `ws://localhost:5001/hubs/auction`

**Infrastructure:**
- RabbitMQ Admin Panel: `http://localhost:15672` (guest/guest)
- SQL Server: `localhost:1433` (sa/Harshid@123)
- Redis CLI: `redis-cli -h localhost -p 6379`

### From Container Network (Service-to-Service)

Services communicate internally using container names:
```
http://admin:5087
http://user:8080
http://product:5088
http://verify:5089
http://auction:5001
http://apigateway:5000
rabbitmq:5672
sqlserver:1433
redis:6379
```

---

## 📁 Dockerfile Structure

All Dockerfiles use **multi-stage builds** for optimization:

### Build Stage
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY *.csproj .
RUN dotnet restore
COPY . .
RUN dotnet build -c Release
```

### Publish Stage
```dockerfile
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish
```

### Runtime Stage
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "*.dll"]
```

**Benefits:**
- ✅ Smaller final image size (no SDK)
- ✅ Faster build times (layer caching)
- ✅ Better security (no build tools in production)
- ✅ Reduced deployment time

---

## 🔧 AUCTION Service Special Configuration

The AUCTION service requires additional setup for **SignalR** and **Redis**:

### Redis Configuration
```yaml
Redis:
  ConnectionString: "redis:6379"
```

### RabbitMQ Configuration
```yaml
RabbitMQ:
  Host: "rabbitmq"
  VHost: "/"
  Username: "guest"
  Password: "guest"
```

### JWT & SignalR
- Token passed via query string: `?access_token=<token>`
- WebSocket endpoint: `/hubs/auction`
- CORS enabled for all origins (development)

### Database Auto-Migration
Runs `db.Database.Migrate()` on startup automatically.

---

## 🐳 Docker Compose Configuration

### Network Architecture
All services connected via bridge network: `microservices-network`

```
┌─────────────────────────────────────────┐
│   microservices-network (bridge)        │
├─────────────────────────────────────────┤
│ ┌──────────┐  ┌──────────┐              │
│ │  Admin   │  │  User    │              │
│ │ :5087    │  │ :8080    │              │
│ └──────────┘  └──────────┘              │
│ ┌──────────┐  ┌──────────┐              │
│ │ Product  │  │ Verify   │              │
│ │ :5088    │  │ :5089    │              │
│ └──────────┘  └──────────┘              │
│ ┌──────────┐  ┌──────────┐              │
│ │ Auction  │  │ Gateway  │              │
│ │ :5001    │  │ :5000    │              │
│ └──────────┘  └──────────┘              │
│ ┌──────────┐  ┌──────────┐  ┌────────┐ │
│ │ SQL Srv  │  │ RabbitMQ │  │ Redis  │ │
│ │ :1433    │  │ :5672    │  │ :6379  │ │
│ └──────────┘  └──────────┘  └────────┘ │
└─────────────────────────────────────────┘
```

### Service Dependencies

Services start in order:
1. **Infrastructure First**: SQL Server, RabbitMQ, Redis (with health checks)
2. **Core Services**: Admin → User → Product → Verify
3. **Specialized Services**: Auction
4. **API Gateway**: Last (depends on all services)

Health checks ensure services are ready before dependents start.

---

## 🗄️ Database Configuration

### Connection String Pattern
```
Server=sqlserver,1433;
Database=<DbName>;
User Id=sa;
Password=Harshid@123;
TrustServerCertificate=True;
```

### Databases Created
- **AdminDb** - Admin & Request data
- **UserDb** - User & authentication data
- **ProductDb** - Product inventory
- **VerifyDb** - Verification records
- **AuctionDb** - Auction & bid data

All use SQL Server 2022 Developer Edition (free for development).

---

## 📬 RabbitMQ Message Broker

### Configuration
- **Host**: rabbitmq (container) / localhost:5672 (localhost)
- **Admin Panel**: http://localhost:15672
- **Credentials**: guest / guest
- **Virtual Host**: /

### Message Types Supported
- Product events (verified, unverified, created, deleted)
- User notifications
- Auction events
- Bid notifications

### Retry Policy
- Automatic retry intervals: 5s, 15s, 30s
- Exponential backoff for failed messages

---

## 💾 Volumes & Persistence

Persistent volumes for data storage:

```yaml
volumes:
  sqlserver_data:      # SQL Server databases
  rabbitmq_data:       # Message queue persistence
  redis_data:          # Cache persistence (AOF enabled)
```

Data survives container restarts but is removed with `docker-compose down -v`.

---

## 🎮 Common Docker Commands

### View Logs
```bash
# All services
docker-compose logs -f

# Specific service
docker-compose logs -f auction
docker-compose logs -f admin

# Last 100 lines
docker-compose logs -f --tail=100

# Follow specific pattern
docker-compose logs -f | grep "ERROR"
```

### Manage Services
```bash
# Restart all
docker-compose restart

# Restart specific
docker-compose restart auction

# Rebuild without cache
docker-compose build --no-cache admin

# Stop all
docker-compose down

# Stop and remove volumes
docker-compose down -v

# Check status
docker-compose ps

# View resource usage
docker stats
```

### Access Containers
```bash
# Enter container shell
docker exec -it auction-service /bin/bash

# Run command in container
docker exec admin-service dotnet ef database update

# View container logs
docker logs -f auction-service
```

### Network Diagnostics
```bash
# Test connectivity between services
docker exec auction-service curl http://admin:5087/api/health

# Check network
docker network inspect microservices-network

# DNS resolution
docker exec auction-service nslookup admin
```

---

## 🔍 Troubleshooting

### Issue: Port Already in Use
```bash
# Find process using port
lsof -i :5001
lsof -i :1433

# Kill process
kill -9 <PID>

# Or change port in docker-compose.yml
ports:
  - "5002:5001"  # Map to different external port
```

### Issue: Database Connection Failed
```bash
# Check SQL Server health
docker-compose logs sqlserver

# Wait longer for startup
sleep 30
docker-compose ps

# Verify connection
docker exec microservice-sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P Harshid@123
```

### Issue: RabbitMQ Connection Failed
```bash
# Check RabbitMQ status
docker-compose logs rabbitmq

# Access admin panel
# http://localhost:15672 (guest/guest)

# Verify service can reach it
docker exec auction-service ping rabbitmq
```

### Issue: Redis Connection Failed
```bash
# Check Redis health
docker-compose logs redis

# Test connection
docker exec microservice-redis redis-cli ping

# Should return: PONG
```

### Issue: Service Won't Start
```bash
# Check detailed logs
docker-compose logs auction

# Rebuild from scratch
docker-compose down -v
docker system prune -a
docker-compose build --no-cache
docker-compose up -d

# Check service health
docker-compose ps
```

### Issue: SignalR Connection Fails (Auction)
```bash
# Verify token format
# Token must be sent as query string:
# ws://localhost:5001/hubs/auction?access_token=<JWT>

# Check CORS is enabled
docker exec auction-service curl -H "Origin: http://localhost:3000" \
  -H "Access-Control-Request-Method: GET" \
  -X OPTIONS http://localhost:5001/hubs/auction

# Verify WebSocket support
docker exec auction-service curl -i -N \
  -H "Connection: Upgrade" \
  -H "Upgrade: websocket" \
  http://localhost:5001/hubs/auction
```

---

## 🔐 Security Notes

### Development vs Production
```yaml
# Current: Development settings
ASPNETCORE_ENVIRONMENT: Development

# Production changes needed:
ASPNETCORE_ENVIRONMENT: Production
# - Change JWT keys
# - Use strong passwords
# - Enable HTTPS
# - Restrict CORS
# - Use secrets management
```

### Passwords to Change
- SQL Server: `Harshid@123` → Strong password
- RabbitMQ: `guest/guest` → Strong credentials
- JWT Key: Update in appsettings

### Best Practices
1. Use Docker Secrets for sensitive data
2. Enable SSL/TLS for all services
3. Implement rate limiting
4. Set resource limits (CPU, memory)
5. Regular security updates

---

## 📈 Performance Tuning

### Build Optimization
```bash
# Cache layers effectively
docker-compose build --parallel

# Use BuildKit for faster builds
DOCKER_BUILDKIT=1 docker-compose build
```

### Runtime Optimization
```yaml
# Add resource limits
services:
  auction:
    deploy:
      resources:
        limits:
          cpus: '1'
          memory: 512M
        reservations:
          cpus: '0.5'
          memory: 256M
```

### Database Optimization
```bash
# Connection pooling in connection string
# Max Pool Size=20;

# Enable query caching in Redis
# Cache frequently accessed data
```

---

## 🧪 Testing & Validation

### Health Check Endpoints
```bash
# Admin Service
curl http://localhost:5087/

# User Service
curl http://localhost:8080/

# Product Service
curl http://localhost:5088/

# Verify Service
curl http://localhost:5089/

# Auction Service
curl http://localhost:5001/

# API Gateway
curl http://localhost:5000/
```

### Test Inter-Service Communication
```bash
# From Product to Admin
docker exec product-service curl http://admin:5087/api/request

# From Auction to Product
docker exec auction-service curl http://product:5088/api/product

# From User to Admin
docker exec user-service curl http://admin:5087/api/request
```

### Test Message Queue
```bash
# Check RabbitMQ connections
docker exec microservice-rabbitmq rabbitmqctl list_connections

# Check message queue stats
docker exec microservice-rabbitmq rabbitmqctl list_queues

# Purge all queues (if needed)
docker exec microservice-rabbitmq rabbitmqctl purge_queue -p / <queue_name>
```

---

## 📚 Additional Resources

- **Docker Docs**: https://docs.docker.com/
- **.NET in Docker**: https://docs.microsoft.com/en-us/dotnet/architecture/microservices/container-docker-introduction/
- **Docker Compose**: https://docs.docker.com/compose/
- **RabbitMQ**: https://www.rabbitmq.com/documentation.html
- **Redis**: https://redis.io/documentation
- **SignalR**: https://docs.microsoft.com/en-us/aspnet/core/signalr/

---

## 📝 Maintenance

### Regular Tasks
```bash
# Weekly: Check logs for errors
docker-compose logs | grep ERROR

# Monthly: Update base images
docker pull mcr.microsoft.com/dotnet/aspnet:10.0
docker pull redis:7-alpine
docker pull rabbitmq:3-management

# Remove unused data
docker system prune -a --volumes
```

### Backup & Recovery
```bash
# Backup database
docker exec microservice-sqlserver /opt/mssql-tools/bin/sqlcmd \
  -S localhost -U sa -P Harshid@123 \
  -Q "BACKUP DATABASE [AdminDb] TO DISK = N'/var/opt/mssql/backup/AdminDb.bak'"

# Backup Redis
docker exec microservice-redis redis-cli BGSAVE
docker cp microservice-redis:/data/dump.rdb ./backup/
```

---

## ✅ Checklist

Before deploying to production:
- [ ] Change all default passwords
- [ ] Update JWT keys to production values
- [ ] Enable HTTPS/SSL certificates
- [ ] Configure resource limits
- [ ] Set up monitoring (Prometheus, ELK)
- [ ] Enable centralized logging
- [ ] Test failover scenarios
- [ ] Setup backup & restore procedures
- [ ] Document API endpoints
- [ ] Configure rate limiting
- [ ] Test load balancing
- [ ] Review security policies
- [ ] Setup CI/CD pipeline
- [ ] Create runbooks for operations team

---

**Last Updated**: March 2026
**Docker Version**: 20.10+
**Docker Compose Version**: 1.29+
**Status**: ✅ All 6 Services Ready for Deployment
