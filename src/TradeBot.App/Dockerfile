FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY TradeBotApp.sln ./
COPY src/TradeBot.App/TradeBot.App.csproj src/TradeBot.App/
RUN dotnet restore src/TradeBot.App/TradeBot.App.csproj

COPY src/TradeBot.App/ src/TradeBot.App/
RUN dotnet publish src/TradeBot.App/TradeBot.App.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends python3 python3-pip python3-venv ca-certificates \
    && rm -rf /var/lib/apt/lists/* \
    && python3 -m venv /opt/tradebot-python \
    && /opt/tradebot-python/bin/pip install --no-cache-dir --upgrade pip \
    && /opt/tradebot-python/bin/pip install --no-cache-dir yfinance pandas numpy

COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://0.0.0.0:5100
ENV PATH="/opt/tradebot-python/bin:${PATH}"
EXPOSE 5100

ENTRYPOINT ["dotnet", "TradeBot.App.dll"]