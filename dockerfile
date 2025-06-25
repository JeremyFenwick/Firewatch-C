# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS builder

WORKDIR /app

# Copy solution and project files
COPY Firewatch-C.sln ./
COPY Firewatch.Core/Firewatch.Core.csproj Firewatch.Core/
COPY Firewatch.Campfires/Firewatch.Campfires.csproj Firewatch.Campfires/

# Restore dependencies
RUN dotnet restore Firewatch-C.sln

# Copy the rest of the source code
COPY . ./

# Publish as a native Linux executable (self-contained)
RUN dotnet publish Firewatch.Core/Firewatch.Core.csproj \
    -c Release \
    -r linux-x64 \
    -p:PublishSingleFile=true \
    -p:EnableCompressionInSingleFile=true \
    -o /publish

# Stage 2: Runtime
FROM debian:bookworm-slim

# Install any required runtime dependencies
RUN apt-get update && apt-get install -y libicu-dev libssl-dev \
    && apt-get clean && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Copy the published binary
COPY --from=builder /publish/ .

# Mark it executable just in case
RUN chmod +x Firewatch.Core

# Expose the port (optional, for Fly.io metadata only)
EXPOSE 5000

# Entry point
ENTRYPOINT ["./Firewatch.Core"]