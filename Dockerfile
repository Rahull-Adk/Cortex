FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Cortex.Domain/Cortex.Domain.csproj Cortex.Domain/
COPY Cortex.Application/Cortex.Application.csproj Cortex.Application/
COPY Cortex.Infrastructure/Cortex.Infrastructure.csproj Cortex.Infrastructure/
COPY Cortex.Api/Cortex.Api.csproj Cortex.Api/
RUN dotnet restore Cortex.Api/Cortex.Api.csproj

COPY . .
RUN dotnet publish Cortex.Api/Cortex.Api.csproj --configuration Release --no-restore --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Cortex.Api.dll"]
