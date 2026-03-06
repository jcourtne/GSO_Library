# Stage 1: Build React SPA
FROM node:22-alpine AS frontend-build
WORKDIR /app/frontend
COPY GSO_Library.Web/package.json GSO_Library.Web/package-lock.json ./
RUN npm ci
COPY GSO_Library.Web/ ./
RUN npm run build

# Stage 2: Build .NET API
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build
WORKDIR /src
COPY GSO_Library.slnx ./
COPY GSO_Library/GSO_Library.csproj GSO_Library/
RUN dotnet restore GSO_Library/GSO_Library.csproj
COPY GSO_Library/ GSO_Library/
RUN dotnet publish GSO_Library/GSO_Library.csproj -c Release -o /app/publish --no-restore

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=backend-build /app/publish ./
COPY --from=frontend-build /app/frontend/dist ./wwwroot/
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "GSO_Library.dll"]
