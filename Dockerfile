# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore em camada propria: so refaz quando um .csproj muda.
COPY Directory.Build.props MedFlow.sln ./
COPY src/MedFlow.Domain/*.csproj          src/MedFlow.Domain/
COPY src/MedFlow.Application/*.csproj     src/MedFlow.Application/
COPY src/MedFlow.Infrastructure/*.csproj  src/MedFlow.Infrastructure/
COPY src/MedFlow.Api/*.csproj             src/MedFlow.Api/
COPY tests/MedFlow.UnitTests/*.csproj        tests/MedFlow.UnitTests/
COPY tests/MedFlow.IntegrationTests/*.csproj tests/MedFlow.IntegrationTests/
RUN dotnet restore src/MedFlow.Api/MedFlow.Api.csproj

COPY . .
RUN dotnet publish src/MedFlow.Api/MedFlow.Api.csproj -c Release -o /app/publish --no-restore

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
RUN adduser --disabled-password --gecos "" --uid 1001 medflow
COPY --from=build /app/publish .
USER medflow
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_gcServer=1
ENTRYPOINT ["dotnet", "MedFlow.Api.dll"]
