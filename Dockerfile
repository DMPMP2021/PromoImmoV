# ---------------------------------------------------
# 1. Étape de Build pour le Frontend (Angular)
# ---------------------------------------------------
FROM node:18 AS frontend-build
WORKDIR /app
COPY package*.json ./
RUN npm install
COPY . .
RUN npm run build -- --configuration production

# ---------------------------------------------------
# 2. Étape de Build pour le Backend (.NET 6 / 7 / 8)
# ---------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS backend-build
WORKDIR /src
COPY ["PromoImmo.csproj", "./"]
RUN dotnet restore "PromoImmo.csproj"
COPY . .
RUN dotnet publish "PromoImmo.csproj" -c Release -o /app/publish

# Copier les fichiers compilés d'Angular dans le répertoire wwwroot du backend
COPY --from=frontend-build /app/dist/promo-immo /app/publish/wwwroot

# ---------------------------------------------------
# 3. Étape de Runtime (Exécution de l'application)
# ---------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS final
WORKDIR /app
COPY --from=backend-build /app/publish .

# pg_dump pour la sauvegarde automatique (client PostgreSQL récent : doit être >= version du serveur Supabase)
# Installation du client PostgreSQL pour pg_dump
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Définir la variable d'environnement pour le port Render
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "PromoImmo.dll"]
