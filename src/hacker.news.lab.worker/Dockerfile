FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY hacker.news.lab.sln ./
COPY src/hacker.news.lab.worker/*.csproj src/hacker.news.lab.worker/
COPY src/hacker.news.lab.application/*.csproj src/hacker.news.lab.application/
COPY src/hacker.news.lab.domain/*.csproj src/hacker.news.lab.domain/
COPY src/hacker.news.lab.infrastructure/*.csproj src/hacker.news.lab.infrastructure/

RUN dotnet restore src/hacker.news.lab.worker/hacker.news.lab.worker.csproj

COPY . .

RUN dotnet publish src/hacker.news.lab.worker/hacker.news.lab.worker.csproj \
    -c Release \
    -o /app/publish 

FROM mcr.microsoft.com/dotnet/runtime:9.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "hacker.news.lab.worker.dll"]
