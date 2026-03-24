FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["User/User.csproj", "User/"]
COPY ["Messaging.Contracts/Messaging.Contracts.csproj", "Messaging.Contracts/"]
RUN dotnet restore "User/User.csproj"

COPY . .
WORKDIR "/src/User"
RUN dotnet publish "User.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "User.dll"]