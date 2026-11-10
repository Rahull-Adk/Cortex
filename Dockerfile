FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/Cortex.Domain/Cortex.Domain.csproj src/Cortex.Domain/
COPY src/Cortex.Application/Cortex.Application.csproj src/Cortex.Application/
COPY src/Cortex.Infrastructure/Cortex.Infrastructure.csproj src/Cortex.Infrastructure/
COPY src/Cortex.Api/Cortex.Api.csproj src/Cortex.Api/
RUN dotnet restore src/Cortex.Api/Cortex.Api.csproj

COPY . .
RUN dotnet publish src/Cortex.Api/Cortex.Api.csproj --configuration Release --no-restore --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Cortex.Api.dll"]
