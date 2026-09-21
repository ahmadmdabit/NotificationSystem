# Build stage
FROM mcr.microsoft.com/dotnet/sdk:3.1-alpine AS build
WORKDIR /src

# Copy solution and project files first for layer caching
COPY NotificationSystem.sln ./
COPY ApiGateway/ApiGateway.csproj ApiGateway/

# Restore dependencies
RUN dotnet restore ApiGateway/ApiGateway.csproj

# Copy source code
COPY . .

# Build and publish
WORKDIR /src/ApiGateway
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:3.1-alpine AS runtime
WORKDIR /app

# Create non-root user
RUN addgroup -S appgroup && adduser -S appuser -G appgroup

# Copy published output
COPY --from=build /app/publish .

# Set ownership
RUN chown -R appuser:appgroup /app

# Switch to non-root
USER appuser

# Expose port
EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=40s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://127.0.0.1:8080/health || exit 1

# Entry point
ENTRYPOINT ["dotnet", "ApiGateway.dll"]
