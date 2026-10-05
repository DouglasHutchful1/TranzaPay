FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY TranzaPay.sln ./
COPY TranzaPay.Domain/TranzaPay.Domain.csproj TranzaPay.Domain/
COPY TranzaPay.Application/TranzaPay.Application.csproj TranzaPay.Application/
COPY TranzaPay.Infrastructure/TranzaPay.Infrastructure.csproj TranzaPay.Infrastructure/
COPY TranzaPay.Api/TranzaPay.Api.csproj TranzaPay.Api/
RUN dotnet restore TranzaPay.Api/TranzaPay.Api.csproj
COPY . .
RUN dotnet publish TranzaPay.Api/TranzaPay.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "TranzaPay.Api.dll"]
