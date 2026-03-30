#!/bin/bash

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${YELLOW}================================${NC}"
echo -e "${YELLOW}Microservices Docker Startup${NC}"
echo -e "${YELLOW}================================${NC}"

# Check if Docker is installed
if ! command -v docker &> /dev/null; then
    echo -e "${RED}Docker is not installed. Please install Docker first.${NC}"
    exit 1
fi

# Check if Docker Compose is installed
if ! command -v docker-compose &> /dev/null; then
    echo -e "${RED}Docker Compose is not installed. Please install it first.${NC}"
    exit 1
fi

echo -e "${GREEN}✓ Docker and Docker Compose are installed${NC}"

# Build images
echo -e "\n${YELLOW}Building Docker images...${NC}"
docker-compose build --no-cache

if [ $? -ne 0 ]; then
    echo -e "${RED}✗ Build failed${NC}"
    exit 1
fi

echo -e "${GREEN}✓ Build successful${NC}"

# Start services
echo -e "\n${YELLOW}Starting all services...${NC}"
docker-compose up -d

if [ $? -ne 0 ]; then
    echo -e "${RED}✗ Failed to start services${NC}"
    exit 1
fi

echo -e "${GREEN}✓ Services started${NC}"

# Wait for services to be ready
echo -e "\n${YELLOW}Waiting for services to be healthy...${NC}"
sleep 20

# Check service status
echo -e "\n${YELLOW}Service Status:${NC}"
docker-compose ps

echo -e "\n${GREEN}================================${NC}"
echo -e "${GREEN}All services are running!${NC}"
echo -e "${GREEN}================================${NC}"
echo -e "\n${YELLOW}Access Points:${NC}"
echo -e "  API Gateway:     ${GREEN}http://localhost:5000${NC}"
echo -e "  Admin Service:   ${GREEN}http://localhost:5087${NC}"
echo -e "  User Service:    ${GREEN}http://localhost:8080${NC}"
echo -e "  Product Service: ${GREEN}http://localhost:5088${NC}"
echo -e "  Verify Service:  ${GREEN}http://localhost:5089${NC}"
echo -e "  Auction Service: ${GREEN}http://localhost:5001${NC}"
echo -e "  Auction SignalR: ${GREEN}ws://localhost:5001/hubs/auction${NC}"

echo -e "\n${YELLOW}Infrastructure:${NC}"
echo -e "  RabbitMQ Admin:  ${GREEN}http://localhost:15672${NC} (guest/guest)"
echo -e "  SQL Server:      ${GREEN}localhost:1433${NC} (sa/Harshid@123)"
echo -e "  Redis Cache:     ${GREEN}localhost:6379${NC}"

echo -e "\n${YELLOW}Useful Commands:${NC}"
echo -e "  View all logs:        ${GREEN}docker-compose logs -f${NC}"
echo -e "  View service logs:    ${GREEN}docker-compose logs -f <service_name>${NC}"
echo -e "  Stop services:        ${GREEN}docker-compose down${NC}"
echo -e "  Remove all volumes:   ${GREEN}docker-compose down -v${NC}"
echo -e "  Restart a service:    ${GREEN}docker-compose restart <service_name>${NC}"
echo -e "  Enter container:      ${GREEN}docker exec -it <container_name> /bin/bash${NC}"
echo -e "  Check service health: ${GREEN}docker-compose ps${NC}"

echo -e "\n${YELLOW}Service Names for Docker Commands:${NC}"
echo -e "  ${GREEN}admin-service${NC}, ${GREEN}user-service${NC}, ${GREEN}product-service${NC}, ${GREEN}verify-service${NC}, ${GREEN}auction-service${NC}, ${GREEN}api-gateway${NC}"
echo -e "  ${GREEN}microservice-sqlserver${NC}, ${GREEN}microservice-rabbitmq${NC}, ${GREEN}microservice-redis${NC}"
