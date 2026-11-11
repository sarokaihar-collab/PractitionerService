# ===== Stage 1: build =====
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 1. Копируем .csproj отдельно — для кэширования restore
COPY ["src/PractitionerService.Domain/PractitionerService.Domain.csproj",         "src/PractitionerService.Domain/"]
COPY ["src/PractitionerService.Application/PractitionerService.Application.csproj","src/PractitionerService.Application/"]
COPY ["src/PractitionerService.Infrastructure/PractitionerService.Infrastructure.csproj","src/PractitionerService.Infrastructure/"]
COPY ["src/PractitionerService.Api/PractitionerService.Api.csproj",               "src/PractitionerService.Api/"]

RUN dotnet restore "src/PractitionerService.Api/PractitionerService.Api.csproj"

# 2. Копируем весь код (включая SeedData в Infrastructure)
COPY . .

WORKDIR "/src/src/PractitionerService.Api"
RUN dotnet publish "PractitionerService.Api.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# ===== Stage 2: runtime =====
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Копируем только артефакты публикации
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

USER $APP_UID

HEALTHCHECK --interval=10s --timeout=3s --start-period=20s --retries=5 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "PractitionerService.Api.dll"]
