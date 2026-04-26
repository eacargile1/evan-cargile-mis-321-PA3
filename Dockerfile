# --- Hosted deployment setup: container image for Fly.io / Railway / Render / Heroku container stack ---
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY CS2TacticalAssistant.Api/CS2TacticalAssistant.Api.csproj CS2TacticalAssistant.Api/
RUN dotnet restore CS2TacticalAssistant.Api/CS2TacticalAssistant.Api.csproj
COPY CS2TacticalAssistant.Api/ CS2TacticalAssistant.Api/
WORKDIR /src/CS2TacticalAssistant.Api
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
# Explicitly copy wwwroot so static files (CSS, JS, images) are always present in the
# runtime image, even if the publish step omits them due to SDK content-item defaults.
COPY --from=build /src/CS2TacticalAssistant.Api/wwwroot ./wwwroot
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "CS2TacticalAssistant.Api.dll"]
