
# Financial Instrument Prices API Documentation

This document provides a complete overview of the project's requirements, architecture, implementation details, and instructions on how to run and test the solution locally.

## Table of Contents
- [Overview](#overview)
- [Requirements](#requirements)
- [Implementation Overview](#implementation-overview)
  - [Controllers](#controllers)
  - [Services and Background Services](#services-and-background-services)
- [Folder Structure](#folder-structure)
- [Configuration & Setup](#configuration--setup)
- [REST API Endpoints](#rest-api-endpoints)
- [WebSocket Service](#websocket-service)
- [Data Sources](#data-sources)
- [Logging & Error Reporting](#logging--error-reporting)
- [Performance Considerations](#performance-considerations)
- [Stress Testing](#stress-testing)
- [Running the Project Locally](#running-the-project-locally)
- [Additional Information](#additional-information)

---

## Overview
The project implements a service that provides both a REST API and a WebSocket service to deliver live financial instrument prices. It leverages public data sources (like Tiingo and Binance) and is built to efficiently manage over 1,000 WebSocket subscribers via batching and asynchronous processing.

---

## Requirements
The project satisfies the following requirements:

1. **REST API:**
   - **List Instruments:** An endpoint to retrieve a list of available financial instruments (e.g., `EURUSD`, `USDJPY`, `BTCUSD`).
   - **Current Price:** An endpoint to get the current price of a specific financial instrument.

2. **WebSocket Service:**
   - **Subscription:** Ability to subscribe to live price updates for specific instruments.
   - **Broadcast:** Broadcast price updates to all connected and subscribed clients.

3. **Data Source:**
   - Connect to public APIs (e.g., [Tiingo WebSocket](https://www.tiingo.com/documentation/websockets/forex) for forex and crypto data or Binance for crypto data).

4. **Performance:**
   - Efficient handling of 1,000+ subscribers using a single connection to the data provider and batching updates when broadcasting.

5. **Logging & Error Reporting:**
   - Implement event and error logging via Serilog (console and file logging).

---

## Implementation Overview

### Controllers
- **FinancialIntrumentController.cs**  
  Exposes two endpoints:
  - `GET /FinancialIntrument/available`: Returns a list of instruments.
  - `GET /FinancialIntrument/instrument/{instrument}/latest-price`: Returns the latest price for a given instrument.

### Services and Background Services

  **Services:**
  - **InstrumentRepository.cs:**  
    Manages an in-memory store for instrument prices and provides lists of available instruments for crypto and forex.

  - **WebSocketHandler.cs:**  
    Handles WebSocket connections, manages client subscriptions, and broadcasts price updates. It uses batching (with a batch size of 100) to efficiently handle over 1,000 subscribers.

  **Background Services:**
  - **CryptoPriceUpdaterService.cs & ForexPriceUpdaterService.cs:**  
    Both services establish a single WebSocket connection to the respective data providers (Tiingo) to receive live price updates. They parse the incoming data, update the in-memory store via the repository, and use the WebSocketHandler to broadcast updates.
    
</details>

---

## Folder Structure
```bash
FinancialInstrumentPrices.API 
├── appsettings.Development.json 
├── appsettings.json 
├── appsettings.Production.json 
├── Dockerfile 
├── Program.cs 
├── Controllers 
│ └── FinancialIntrumentController.cs 
└── Properties 
	└── launchSettings.json

FinancialInstrumentPrices.Domain 
├── Constants 
│ └── ApplicationConstants.cs 
├── Contracts 
│ ├── IInstrumentRepository.cs 
│ └── IWebSocketHandler.cs 
├── Models 
  └── PriceDetails.cs 
  └── Options 
  └── DataSourcesOptions.cs

FinancialInstrumentPrices.Infrastructure 
├── BackgroundServices 
│ ├── CryptoPriceUpdaterService.cs 
│ └── ForexPriceUpdaterService.cs 
└── Services
  ├── InstrumentRepository.cs 
  └── WebSocketHandler.cs

FinancialInstruments.StressTesting 
└── Program.cs
```

---

## Configuration & Setup
- **AppSettings:**  
  Configuration files (`appsettings.json`, etc.) data source URIs (e.g., Tiingo WebSocket URIs), and token.  
- **Serilog:**  
  Configured in `Program.cs` to log events and errors to both the console and daily rolling log files.
- **Swagger:**  
  Swagger is enabled for interactive testing of the REST API endpoints.

---

## WebSocket Service

The WebSocket endpoint is mapped to `/ws` in `Program.cs`. Clients can:

-   **Connect:**  
    Open a WebSocket connection at `ws://<host>:<port>/ws`.
    
-   **Subscribe:**  
    Send a JSON message in the following format to subscribe:
```json
{
  "action": "subscribe",
  "instruments": ["btcusdt", "xrpusdt"]
}
```
**Unsubscribe:**  
Send a message with `"action": "unsubscribe"`.

The `WebSocketHandler` class manages client subscriptions and broadcasts price updates in batches to ensure efficient processing.

## REST API Endpoints
Endpoints are defined in the `FinancialIntrumentController`:

- **GET `/FinancialIntrument/available`**  
  Retrieves a list of available instruments.
  
- **GET `/FinancialIntrument/instrument/{instrument}/latest-price`**  
  Retrieves the current price of a specified instrument.

**Example Request:**
```bash
curl https://localhost:7056/FinancialIntrument/available
```


## Data Sources

-   **Tiingo:**  
    Used for both crypto and forex data. The application subscribes to multiple streams using a single WebSocket connection for each market.
    
Configuration for these data sources is handled via the `DataSourcesOptions` class.

## Logging & Error Reporting

-   **Serilog Integration:**  
    The application uses Serilog for logging important events and errors. Logs are written to the console and to files in the `logs` directory.
-   **Error Handling:**  
    Each service (REST API, WebSocket handling, and background price updaters) contains try-catch blocks with appropriate error logging.
## Performance Considerations

-   **Single Connection for Data Providers:**  
    Each price updater service maintains a single WebSocket connection to the data provider.
-   **Batching of WebSocket Messages:**  
    The WebSocketHandler broadcasts updates in batches (e.g., batches of 100) to efficiently handle a large number of subscribers.
-   **Asynchronous Processing:**  
    The use of asynchronous programming (async/await) throughout ensures non-blocking operations.


## Stress Testing

The `FinancialInstruments.StressTesting` project uses NBomber to simulate a high-load scenario:

-   **Scenario Details:**
    
    -   Each virtual user opens a WebSocket connection.
    -   Sends a subscribe message.
    -   Remains connected for a specified duration (e.g., 30 seconds initially, with a simulation that scales up to 1,000 connections for 5 minutes).
-   **How to Run:** Navigate to the `FinancialInstruments.StressTesting` folder and run:
```bash
dotnet run
```

## Running the Project Locally

### Prerequisites

-   [.NET 8 or later](https://dotnet.microsoft.com/download)
-   [VS Code](https://code.visualstudio.com/) (recommended for MacOS)
-   (Optional) Docker if you prefer containerized deployment

### Steps

1.  **Clone the Repository:**

```bash
git clone https://github.com/naelghannam18/FinancialInstrumentsProject.git

Backend
--------
cd FinancialInstrumentPrices.API

Stresstest
-----------
cd FinancialInstruments.StressTesting
```
2. **Restore and build**
```bash
dotnet restore
dotnet build
```

3. **Run the application**
```bash
dotnet run
```

**Test the API:**

-   Open your browser and navigate to Swagger UI for interactive testing.


## Additional Information

-   **API Key**  
   Tiingo token should be provided securely. It is not included in the repository and can be passed via environment variables or configuration files.
-   **Docker Support:**  
    The presence of a `Dockerfile` in the API project allows for containerized deployment if needed.
-   **Code Comments:**  
    The code is thoroughly commented to help you understand the implementation details and design decisions.
