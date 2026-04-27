FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/AuditLogService.Api/AuditLogService.Api.csproj", "src/AuditLogService.Api/"]
COPY ["src/AuditLogService.Application/AuditLogService.Application.csproj", "src/AuditLogService.Application/"]
COPY ["src/AuditLogService.Infrastructure/AuditLogService.Infrastructure.csproj", "src/AuditLogService.Infrastructure/"]
COPY ["src/AuditLogService.Domain/AuditLogService.Domain.csproj", "src/AuditLogService.Domain/"]
RUN dotnet restore "src/AuditLogService.Api/AuditLogService.Api.csproj"

COPY . .
WORKDIR "/src/src/AuditLogService.Api"
RUN dotnet build "AuditLogService.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AuditLogService.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AuditLogService.Api.dll"]
