# AsyncAPI Aspire Example — Mini Shop

A distributed microservices example built with [.NET Aspire](https://learn.microsoft.com/dotnet/aspire/) demonstrating how to use **Bielu.AspNetCore.AsyncApi** across multiple services with a YARP API Gateway that merges AsyncAPI documentation from all downstream microservices.

## Architecture

```
┌──────────────────────────────────────────────────────────┐
│                    Aspire AppHost                         │
│               (Orchestrates everything)                   │
├──────────────────────────────────────────────────────────┤
│                                                          │
│  ┌──────────────┐  ┌───────────┐  ┌────────┐  ┌──────┐ │
│  │  PostgreSQL   │  │   Kafka   │  │ Valkey │  │Ozone │ │
│  │  (ordersdb,   │  │ (broker)  │  │(cache) │  │(S3)  │ │
│  │  inventorydb) │  │           │  │        │  │      │ │
│  └──────┬───────┘  └─────┬─────┘  └───┬────┘  └──┬───┘ │
│         │                │             │           │     │
│  ┌──────┴────────────────┴─────────────┴───────────┤     │
│  │                                                 │     │
│  │  ┌─────────────────┐  ┌──────────────────────┐  │     │
│  │  │  Order Service   │  │  Inventory Service   │  │     │
│  │  │  /asyncapi/v1    │  │  /asyncapi/v1        │  │     │
│  │  │  Kafka producer  │  │  Kafka consumer      │  │     │
│  │  │  PostgreSQL      │  │  PostgreSQL           │  │     │
│  │  │  Valkey cache    │  │  Ozone storage        │  │     │
│  │  └────────┬─────────┘  └──────────┬──────────┘  │     │
│  │           │                       │              │     │
│  │  ┌────────┴───────────────────────┴──────────┐   │     │
│  │  │           API Gateway (YARP)               │   │     │
│  │  │  /asyncapi/merged.json  ← merged docs     │   │     │
│  │  │  /asyncapi              ← AsyncAPI UI      │   │     │
│  │  │  /api/orders/*          → Order Service    │   │     │
│  │  │  /api/inventory/*       → Inventory Svc    │   │     │
│  │  └────────────────────────────────────────────┘   │     │
│  └───────────────────────────────────────────────────┘     │
└──────────────────────────────────────────────────────────┘
```

## Projects

| Project | Description |
|---------|-------------|
| **AppHost** | Aspire orchestrator — wires Kafka, PostgreSQL, Valkey, Ozone (multi-container), and all microservices |
| **ServiceDefaults** | Shared configuration: OpenTelemetry, health checks, service discovery, resilience |
| **OrderService** | Manages orders, publishes `OrderCreated` and `OrderStatusChanged` events to Kafka |
| **InventoryService** | Manages inventory, subscribes to order events, publishes `InventoryReserved` and `StockLevelChanged` events |
| **ApiGateway** | YARP reverse proxy that routes to microservices and serves merged AsyncAPI documentation |

## Technologies

| Technology | Integration | Description |
|------------|------------|-------------|
| **Kafka** | `Aspire.Hosting.Kafka` | Message broker for event-driven communication between services |
| **PostgreSQL** | `Aspire.Hosting.PostgreSQL` | Relational database for order and inventory data |
| **Valkey** | `Aspire.Hosting.Valkey` | Distributed cache (Redis-compatible) for Order and Inventory Services |
| **Apache Ozone** | Custom containers (`AddContainer`) | S3-compatible distributed object storage (SCM, OM, DataNode, S3 Gateway) for Inventory Service |
| **YARP** | `Yarp.ReverseProxy` NuGet | API Gateway / reverse proxy for routing and AsyncAPI doc aggregation |

## AsyncAPI Integration

Each microservice independently exposes its own AsyncAPI documentation:
- **Order Service**: `http://orderservice/asyncapi/v1.json`
- **Inventory Service**: `http://inventoryservice/asyncapi/v1.json`

The **API Gateway** uses **Bielu.AspNetCore.AsyncApi.Merger** to fetch and merge all AsyncAPI documents into a single unified specification, available at:
- **Merged document**: `/asyncapi/merged.json`
- **AsyncAPI UI**: `/asyncapi`

## Running

### Prerequisites
- .NET 10.0 SDK
- Docker (for Aspire container orchestration)

### Start the application
```bash
dotnet run --project src/examples/aspire/Bielu.AspNetCore.AsyncApi.Aspire.AppHost
```

The Aspire dashboard will be available at the URL shown in the console output. From there you can:
1. View all running services and their health status
2. Access the API Gateway's merged AsyncAPI UI
3. Monitor OpenTelemetry traces and logs

### Access AsyncAPI documentation
- **Gateway merged docs**: `http://localhost:5182/asyncapi/merged.json`
- **Gateway AsyncAPI UI**: `http://localhost:5182/asyncapi`
- **Order Service docs**: `http://localhost:5180/asyncapi/v1.json`
- **Inventory Service docs**: `http://localhost:5181/asyncapi/v1.json`

## Kafka Topics

| Topic | Producer | Consumer | Event Type |
|-------|----------|----------|------------|
| `orders.created` | Order Service | Inventory Service | `OrderCreatedEvent` |
| `orders.status-changed` | Order Service | — | `OrderStatusChangedEvent` |
| `inventory.reserved` | Inventory Service | — | `InventoryReservedEvent` |
| `inventory.stock-level-changed` | Inventory Service | — | `StockLevelChangedEvent` |

## Example Merged AsyncAPI Document

The merged AsyncAPI document served by the API Gateway at `/asyncapi/merged.json` combines all microservice specs into a single document:

```json
{
  "asyncapi": "3.0.0",
  "info": {
    "title": "Mini Shop",
    "version": "1.0.0",
    "description": "Merged AsyncAPI documentation from all Mini Shop microservices, served via YARP API Gateway.",
    "license": {
      "name": "Apache 2.0",
      "url": "https://www.apache.org/licenses/LICENSE-2.0"
    }
  },
  "defaultContentType": "application/json",
  "servers": {
    "kafka": {
      "host": "kafka:9092",
      "protocol": "kafka",
      "description": "Apache Kafka broker"
    }
  },
  "channels": {
    "orders.created": {
      "address": "orders.created",
      "servers": [{ "$ref": "#/servers/kafka" }],
      "messages": {
        "OrderCreatedEvent": {
          "$ref": "#/components/messages/OrderCreatedEvent"
        }
      }
    },
    "orders.status-changed": {
      "address": "orders.status-changed",
      "servers": [{ "$ref": "#/servers/kafka" }],
      "messages": {
        "OrderStatusChangedEvent": {
          "$ref": "#/components/messages/OrderStatusChangedEvent"
        }
      }
    },
    "inventory.reserved": {
      "address": "inventory.reserved",
      "servers": [{ "$ref": "#/servers/kafka" }],
      "messages": {
        "InventoryReservedEvent": {
          "$ref": "#/components/messages/InventoryReservedEvent"
        }
      }
    },
    "inventory.stock-level-changed": {
      "address": "inventory.stock-level-changed",
      "servers": [{ "$ref": "#/servers/kafka" }],
      "messages": {
        "StockLevelChangedEvent": {
          "$ref": "#/components/messages/StockLevelChangedEvent"
        }
      }
    }
  },
  "operations": {
    "OrderCreated": {
      "action": "send",
      "channel": { "$ref": "#/channels/orders.created" },
      "messages": [{ "$ref": "#/channels/orders.created/messages/OrderCreatedEvent" }]
    },
    "OrderStatusChanged": {
      "action": "send",
      "channel": { "$ref": "#/channels/orders.status-changed" },
      "messages": [{ "$ref": "#/channels/orders.status-changed/messages/OrderStatusChangedEvent" }]
    },
    "InventoryReserved": {
      "action": "send",
      "channel": { "$ref": "#/channels/inventory.reserved" },
      "messages": [{ "$ref": "#/channels/inventory.reserved/messages/InventoryReservedEvent" }]
    },
    "StockLevelChanged": {
      "action": "send",
      "channel": { "$ref": "#/channels/inventory.stock-level-changed" },
      "messages": [{ "$ref": "#/channels/inventory.stock-level-changed/messages/StockLevelChangedEvent" }]
    }
  },
  "components": {
    "messages": {
      "OrderCreatedEvent": {
        "payload": {
          "type": "object",
          "properties": {
            "orderId": { "type": "string", "format": "uuid" },
            "productId": { "type": "string" },
            "quantity": { "type": "integer" },
            "timestamp": { "type": "string", "format": "date-time" }
          }
        }
      },
      "OrderStatusChangedEvent": {
        "payload": {
          "type": "object",
          "properties": {
            "orderId": { "type": "string", "format": "uuid" },
            "previousStatus": { "type": "string" },
            "newStatus": { "type": "string" },
            "timestamp": { "type": "string", "format": "date-time" }
          }
        }
      },
      "InventoryReservedEvent": {
        "payload": {
          "type": "object",
          "properties": {
            "orderId": { "type": "string", "format": "uuid" },
            "productId": { "type": "string" },
            "quantityReserved": { "type": "integer" },
            "success": { "type": "boolean" },
            "timestamp": { "type": "string", "format": "date-time" }
          }
        }
      },
      "StockLevelChangedEvent": {
        "payload": {
          "type": "object",
          "properties": {
            "productId": { "type": "string" },
            "previousQuantity": { "type": "integer" },
            "newQuantity": { "type": "integer" },
            "reason": { "type": "string" },
            "timestamp": { "type": "string", "format": "date-time" }
          }
        }
      }
    }
  }
}
```
