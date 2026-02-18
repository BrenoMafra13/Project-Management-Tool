FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy the project file and restore dependencies
COPY WebApplication1/WebApplication1.csproj ./WebApplication1/
RUN dotnet restore ./WebApplication1/WebApplication1.csproj

# Copy remaining files and build
COPY WebApplication1/ ./WebApplication1/
RUN dotnet publish ./WebApplication1/WebApplication1.csproj -c Release -o /app/publish

# Final stage - runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# Install PostgreSQL client for database operations
RUN apt-get update && apt-get install -y postgresql-client \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1

EXPOSE 8080

# Start the application
ENTRYPOINT ["dotnet", "WebApplication1.dll"]

