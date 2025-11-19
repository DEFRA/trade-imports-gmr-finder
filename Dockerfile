# Base dotnet image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

# Add curl to template.
# CDP PLATFORM HEALTHCHECK REQUIREMENT
RUN apt update && \
    apt install curl -y && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Build stage image
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY .config/dotnet-tools.json .config/dotnet-tools.json
COPY .csharpierrc .csharpierrc

RUN dotnet tool restore
RUN dotnet csharpier check .

COPY src/Domain/Domain.csproj src/Domain/Domain.csproj
COPY src/GvmsClient/GvmsClient.csproj src/GvmsClient/GvmsClient.csproj
COPY src/GmrFinder/GmrFinder.csproj src/GmrFinder/GmrFinder.csproj
COPY tests/GvmsClient.Tests/GvmsClient.Tests.csproj tests/GvmsClient.Tests/GvmsClient.Tests.csproj
COPY tests/GmrFinder.Tests/GmrFinder.Tests.csproj tests/GmrFinder.Tests/GmrFinder.Tests.csproj
COPY tests/GmrFinder.IntegrationTests/GmrFinder.IntegrationTests.csproj tests/GmrFinder.IntegrationTests/GmrFinder.IntegrationTests.csproj
COPY tests/TestFixtures/TestFixtures.csproj tests/TestFixtures/TestFixtures.csproj
COPY GmrFinder.slnx GmrFinder.slnx
COPY Directory.Build.props Directory.Build.props
COPY NuGet.config NuGet.config

RUN dotnet restore

COPY src/Domain src/Domain
COPY src/GvmsClient src/GvmsClient
COPY src/GmrFinder src/GmrFinder
COPY tests/GvmsClient.Tests tests/GvmsClient.Tests
COPY tests/GmrFinder.Tests tests/GmrFinder.Tests
COPY tests/GmrFinder.IntegrationTests tests/GmrFinder.IntegrationTests
COPY tests/TestFixtures tests/TestFixtures

RUN dotnet test --no-restore --warnaserror --filter "Category!=IntegrationTests"

FROM build AS publish

RUN dotnet publish src/GmrFinder -c Release -warnaserror -o /app/publish /p:UseAppHost=false

ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true

# Final production image
FROM base AS final
WORKDIR /app

COPY --from=publish /app/publish .

EXPOSE 8085
ENTRYPOINT ["dotnet", "GmrFinder.dll"]
