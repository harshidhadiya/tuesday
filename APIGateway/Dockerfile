FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files and restore
COPY ["APIGateway/APIGateway.csproj", "APIGateway/"]
RUN dotnet restore "APIGateway/APIGateway.csproj"

# Copy source and build
COPY . .
WORKDIR "/src/APIGateway"
RUN dotnet publish "APIGateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "APIGateway.dll"]