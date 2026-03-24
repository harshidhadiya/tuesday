FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files and restore
COPY ["Verify/Verify.csproj", "Verify/"]
COPY ["Messaging.Contracts/Messaging.Contracts.csproj", "Messaging.Contracts/"]
RUN dotnet restore "Verify/Verify.csproj"

# Copy source and build
COPY . .
WORKDIR "/src/Verify"
RUN dotnet publish "Verify.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Verify.dll"]