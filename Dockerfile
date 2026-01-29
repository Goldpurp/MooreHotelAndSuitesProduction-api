# ---------- Build Stage ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY *.sln .
COPY MooreHotels.Api/*.csproj MooreHotels.Api/
COPY MooreHotels.Infrastructure/*.csproj MooreHotels.Infrastructure/
COPY MooreHotels.Application/*.csproj MooreHotels.Application/
COPY MooreHotels.Domain/*.csproj MooreHotels.Domain/

RUN dotnet restore

COPY . .
WORKDIR /src/MooreHotels.Api

RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# ---------- Runtime Stage ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Render requires PORT environment variable
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["dotnet", "MooreHotels.Api.dll"]
