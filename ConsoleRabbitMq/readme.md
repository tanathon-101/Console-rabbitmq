# ConsoleRabbitMq

A simple .NET console application that demonstrates publishing messages to RabbitMQ using dependency injection and Polly retry policy.

---

## 📦 Features

- Connects to RabbitMQ using `RabbitMQ.Client`
- Publishes a simple string message to a queue
- Uses Polly for automatic retry on failure
- Demonstrates invalid parallel pipelining scenario (for test)

---

## 🚀 Prerequisites

- [.NET 6+](https://dotnet.microsoft.com/en-us/download)
- [Docker](https://www.docker.com/) (to run RabbitMQ easily)

---

## 🐇 Run RabbitMQ with Docker

```bash
docker run -d --hostname rabbitmq --name rabbitmq-dev \
  -p 5672:5672 -p 15672:15672 \
  rabbitmq:3-management
