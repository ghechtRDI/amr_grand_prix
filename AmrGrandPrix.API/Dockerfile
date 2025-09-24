# Development Dockerfile for .NET API
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS development

WORKDIR /app

# Install Node.js for SPA proxy support
RUN curl -fsSL https://deb.nodesource.com/setup_20.x | bash - && \
    apt-get install -y nodejs

# Copy project file and restore dependencies
COPY *.csproj ./
RUN dotnet restore

# Copy source code
COPY . ./

# Expose ports
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Development
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_SKIP_FIRST_TIME_EXPERIENCE=true

# Start the application
CMD ["dotnet", "run", "--urls", "http://+:8080"]