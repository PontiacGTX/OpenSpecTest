FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY OpenSpecTest/OpenSpec.API.csproj OpenSpecTest/
COPY OpenSpec.Application/OpenSpec.Application.csproj OpenSpec.Application/
COPY OpenSpec.Domain/OpenSpec.Domain.csproj OpenSpec.Domain/
COPY OpenSpec.Infraestructure/OpenSpec.Infraestructure.csproj OpenSpec.Infraestructure/
COPY OpenSpec.Common/OpenSpec.Common.csproj OpenSpec.Common/
RUN dotnet restore OpenSpecTest/OpenSpec.API.csproj

COPY . .
RUN dotnet publish OpenSpecTest/OpenSpec.API.csproj \
    --configuration Release \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OpenSpec.API.dll"]