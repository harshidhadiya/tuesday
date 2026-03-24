FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG SERVICE_NAME
WORKDIR /src

# Copy everything and restore/build the specific service
COPY . .
RUN dotnet restore "${SERVICE_NAME}/${SERVICE_NAME}.csproj"
RUN dotnet publish "${SERVICE_NAME}/${SERVICE_NAME}.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Set the entrypoint to the service's DLL
ARG SERVICE_NAME
ENV SERVICE_DLL=${SERVICE_NAME}.dll
ENTRYPOINT ["sh", "-c", "dotnet $SERVICE_DLL"]
