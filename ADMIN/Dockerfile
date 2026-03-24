FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files and restore
COPY ["ADMIN/ADMIN.csproj", "ADMIN/"]
COPY ["Messaging.Contracts/Messaging.Contracts.csproj", "Messaging.Contracts/"]
RUN dotnet restore "ADMIN/ADMIN.csproj"

# Copy source and build
COPY . .
WORKDIR "/src/ADMIN"
RUN dotnet publish "ADMIN.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ADMIN.dll"]