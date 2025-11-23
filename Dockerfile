# Use the official .NET 8 runtime as base image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
RUN apt-get update && apt-get install -y tesseract-ocr libtesseract-dev libleptonica-dev
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Use the official .NET 8 SDK for building
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["DocTask.Api/DocTask.Api.csproj", "DocTask.Api/"]
COPY ["DocTask.Core/DocTask.Core.csproj", "DocTask.Core/"]
COPY ["DocTask.Data/DocTask.Data.csproj", "DocTask.Data/"]
COPY ["DocTask.Service/DocTask.Service.csproj", "DocTask.Service/"]

# Restore dependencies
RUN dotnet restore "DocTask.Api/DocTask.Api.csproj"

# Copy all source code
COPY . .

# Build the application
WORKDIR "/src/DocTask.Api"
RUN dotnet build "DocTask.Api.csproj" -c Release -o /app/build

# Publish the application
FROM build AS publish
RUN dotnet publish "DocTask.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Create final runtime image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Set environment variables for Railway
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "DocTask.Api.dll"]
