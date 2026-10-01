# Multi-stage build for Render (or any container host). The app binds to $PORT when set.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so package layers cache across source-only changes.
COPY src/OpportunityPilot.Domain/OpportunityPilot.Domain.csproj src/OpportunityPilot.Domain/
COPY src/OpportunityPilot.Application/OpportunityPilot.Application.csproj src/OpportunityPilot.Application/
COPY src/OpportunityPilot.Infrastructure/OpportunityPilot.Infrastructure.csproj src/OpportunityPilot.Infrastructure/
COPY src/OpportunityPilot.Api/OpportunityPilot.Api.csproj src/OpportunityPilot.Api/
RUN dotnet restore src/OpportunityPilot.Api/OpportunityPilot.Api.csproj

COPY src/ src/
RUN dotnet publish src/OpportunityPilot.Api/OpportunityPilot.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
# Npgsql probes GSSAPI on connect; without this library every connection logs a harmless but alarming error.
RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*
COPY --from=build /app .
ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true
USER $APP_UID
EXPOSE 8080
# Migrations run as their own process before the server starts, never inside it.
# Set MIGRATE_ON_START=true where the host has no pre-deploy hook (Render free tier).
# If migration fails the container exits and the previous deploy keeps serving.
ENV MIGRATE_ON_START=false
ENTRYPOINT ["/bin/sh", "-c", "if [ \"$MIGRATE_ON_START\" = \"true\" ]; then dotnet OpportunityPilot.Api.dll --migrate || exit 1; fi; exec dotnet OpportunityPilot.Api.dll"]
