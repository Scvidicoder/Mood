# Mood Pickup System

## Project Overview

**Project name:** Mood Pickup System

**Customer:** Mood Dushanbe

## Purpose

Mood Pickup System is a web platform for the Mood Dushanbe café that
allows customers to place orders in advance, pay online or on pickup,
track preparation in real time, and collect their order without waiting
in line.

The project also provides internal tools for café employees to manage
orders, kitchen workflow, menu, and café settings.

## Goals

### Customer goals

-   Order food and drinks in advance.
-   Choose "As soon as possible" or a pickup time.
-   Pay online or on pickup.
-   Track the order status in real time.
-   Receive Telegram notifications.
-   Repeat previous orders.

### Business goals

-   Reduce queues.
-   Improve customer experience.
-   Simplify employee workflow.
-   Centralize menu management.
-   Speed up order processing.

## Target users

-   Customer
-   Employee
-   Administrator

Employees may have multiple roles simultaneously.

## Version 1 Scope

Included:

-   Customer registration using phone number and Telegram verification.
-   Personal profile.
-   Product catalog.
-   Flexible product configuration (sizes, milk, syrups, etc.).
-   Shopping cart.
-   Order management.
-   Online payment abstraction.
-   Pay on pickup.
-   Real-time updates using SignalR.
-   Telegram notifications.
-   Employee dashboard.
-   Kitchen board.
-   Pickup board.
-   Menu management.
-   Employee management.
-   Working hours configuration.
-   Order history.

Not included:

-   Delivery.
-   Loyalty program.
-   Promo codes.
-   Reviews.
-   Multiple cafés.
-   Dark mode.
-   Multi-language support.

## Technology Stack

### Backend

-   ASP.NET Core Web API
-   Entity Framework Core
-   PostgreSQL
-   SignalR
-   JWT + Refresh Tokens
-   Telegram Bot API
-   FluentValidation
-   Swagger

### Frontend

-   React
-   TypeScript
-   Redux Toolkit

### Infrastructure

-   Docker Compose

## Repository Structure

``` text
backend/
frontend/
docs/
docker/
```

## Documentation

This repository contains detailed documentation covering:

-   Product vision
-   Business rules
-   User flows
-   Database design
-   REST API
-   Authentication
-   Telegram integration
-   Notifications
-   UI specifications
-   Architecture
-   Deployment
-   Roadmap

This document serves as the entry point for the entire project
documentation.
