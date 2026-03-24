FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files and restore
COPY ["Product/Product.csproj", "Product/"]
COPY ["Messaging.Contracts/Messaging.Contracts.csproj", "Messaging.Contracts/"]
COPY ["CloudinaryService/CloudinaryService.csproj", "CloudinaryService/"]
RUN dotnet restore "Product/Product.csproj"

# Copy source and build
COPY . .
WORKDIR "/src/Product"
RUN dotnet publish "Product.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Product.dll"]