FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Admissions.csproj ./
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
# Run as a non-root user for a smaller attack surface.
RUN groupadd -r appuser && useradd -r -g appuser -m appuser \
    && mkdir -p /app/uploads && chown -R appuser:appuser /app
WORKDIR /app
COPY --from=build --chown=appuser:appuser /app .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
USER appuser
ENTRYPOINT ["dotnet", "Admissions.dll"]
