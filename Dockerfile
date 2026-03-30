# Stage 1: Build Frontend
FROM node:20-alpine AS build-frontend
WORKDIR /app/client
COPY redditanalyzer.client/package*.json ./
RUN npm install
COPY redditanalyzer.client/ ./
RUN npm run build

# Stage 2: Build Backend
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-backend
WORKDIR /app
COPY RedditAnalyzer.Server/RedditAnalyzer.Server.csproj RedditAnalyzer.Server/
# Remove reference to the .esproj to avoid issues in Docker
RUN sed -i '/<ProjectReference/,/<\/ProjectReference>/d' RedditAnalyzer.Server/RedditAnalyzer.Server.csproj
RUN dotnet restore RedditAnalyzer.Server/RedditAnalyzer.Server.csproj
COPY RedditAnalyzer.Server/ RedditAnalyzer.Server/
# Copy built frontend to wwwroot
COPY --from=build-frontend /app/client/dist /app/RedditAnalyzer.Server/wwwroot
RUN dotnet publish RedditAnalyzer.Server/RedditAnalyzer.Server.csproj -c Release -o /publish

# Stage 3: Final Image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build-backend /publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "RedditAnalyzer.Server.dll"]
