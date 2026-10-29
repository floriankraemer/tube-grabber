# Dev image: run builds/tests via Makefile with the repo bind-mounted at /src.
#
#   docker compose build
#   docker compose run --rm dev make ci
#
# Projects target net10.0; YtDlpUi.slnx requires .NET SDK 9.0.200+ (MSBuild slnx support).
FROM mcr.microsoft.com/dotnet/sdk:10.0

ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update \
    && apt-get install -y --no-install-recommends git make curl ca-certificates \
    && rm -rf /var/lib/apt/lists/*

# Stryker.NET — see docs/build.md; `make mutation-test` uses `dotnet-stryker`.
RUN dotnet tool install -g dotnet-stryker --version 5.0.0

ENV PATH="${PATH}:/root/.dotnet/tools"

WORKDIR /src

CMD ["make", "help"]
