# fem2d - FEALiTE2D.Api (stateless REST: GET /health, POST /api/v1/analyze) as a Linux container.
#   docker build -t fem2d:<version> .
#   docker run -d -p 9300:8080 fem2d:<version>
# Host network (NAS): -e ASPNETCORE_URLS=http://127.0.0.1:9300 - only the local web containers call it.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# restore first (cached while only sources change)
COPY global.json Directory.Build.props ./
COPY FEALiTE2D/FEALiTE2D.csproj FEALiTE2D/
COPY FEALiTE2D.Api.Contracts/FEALiTE2D.Api.Contracts.csproj FEALiTE2D.Api.Contracts/
COPY FEALiTE2D.Api/FEALiTE2D.Api.csproj FEALiTE2D.Api/
RUN dotnet restore FEALiTE2D.Api/FEALiTE2D.Api.csproj
COPY FEALiTE2D/ FEALiTE2D/
COPY FEALiTE2D.Api.Contracts/ FEALiTE2D.Api.Contracts/
COPY FEALiTE2D.Api/ FEALiTE2D.Api/
# no NuGet packages / XML docs in the image build
RUN dotnet publish FEALiTE2D.Api/FEALiTE2D.Api.csproj -c Release --no-restore -o /app \
      -p:GeneratePackageOnBuild=false -p:GenerateDocumentationFile=false -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
COPY FEALiTE2D.Api/smoke.json /app/smoke.json
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_gcServer=0
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "FEALiTE2D.Api.dll"]
