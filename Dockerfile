# Stage 1: Build the application
# Use the official .NET SDK image, specifying the version from global.json
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the project files and restore dependencies
COPY ["*.csproj", "./"]
RUN dotnet restore

# Copy the rest of the application source code
COPY . .
WORKDIR "/src/."
# Build the application in Release mode
RUN dotnet build "InventoryManagementSystem.csproj" -c Release -o /app/build

# Stage 2: Publish the application
FROM build AS publish
RUN dotnet publish "InventoryManagementSystem.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Create the final runtime image
# Use the smaller ASP.NET runtime image for the final stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Expose port 8080. Railway provides the PORT env var, which ASPNETCORE_URLS will use.
ENV ASPNETCORE_URLS=http://*:8080

ENTRYPOINT ["dotnet", "InventoryManagementSystem.dll"]