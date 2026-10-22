FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY global.json ./
COPY Backend/ ./Backend/
RUN dotnet publish Backend/API/Badminton.API.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
COPY deploy/certificates/supabase-ca.crt ./certificates/supabase-ca.crt
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_HTTP_PORTS=8080
ENV Logging__FileDirectory=/tmp/badminton-logs
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "Badminton.API.dll"]
