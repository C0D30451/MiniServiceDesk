# MiniServiceDesk
MiniServiceDesk e una piattaforma ticketing full-stack implementata con .NET 8 con:
- API REST ASP.NET Core + Entity Framework Core (SQLite)
- Frontend Blazor Server
- Autenticazione JWT + ruoli (`User`, `Agent`, `Admin`)
- Board Kanban con colonne personali e drag & drop dei ticket

## Architettura
- `MiniServiceDesk/MiniServiceDesk.Api`: backend REST, auth, gestione ticket, dashboard e utenti.
- `MiniServiceDesk/MiniServiceDesk.Web`: interfaccia Blazor Server.
- Database SQLite locale: `mini_service_desk.db` (creato automaticamente dall'API).

## Prerequisiti
- .NET SDK 8 (il repository include `global.json`).

## Avvio rapido (locale)
Apri due terminali nella root del repository.

1. Avvia API:
```bash
dotnet run --project MiniServiceDesk/MiniServiceDesk.Api
```
Endpoint locali configurati: `https://localhost:7219` e `http://localhost:5129`.

2. Avvia Web:
```bash
dotnet run --project MiniServiceDesk/MiniServiceDesk.Web
```
Endpoint locali configurati: `https://localhost:7132` e `http://localhost:5021`.

3. Accedi all'app:
- Web UI: `https://localhost:7132`
- Swagger API: `https://localhost:7219/swagger`

## Seed automatico
All'avvio dell'API vengono applicate le migration e creati utenti demo:
- `demo.user` / `Passw0rd!` (ruolo `User`)
- `demo.agent` / `Passw0rd!` (ruolo `Agent`)
- `demo.admin` / `Passw0rd!` (ruolo `Admin`)

## Funzionalita principali
- Login JWT e autorizzazione per ruolo.
- Creazione ticket e gestione dettagli/commenti.
- Board personale con colonne custom.
- Drag & drop per spostamento tra colonne e riordino verticale ticket.
- Vista globale ticket con filtri per Agent/Admin.
- Dashboard riepilogo e workload agent.
- Configurazione utenti/ruoli per Admin.

## API principali
- `POST /api/auth/login`
- `GET /api/tickets`
- `GET /api/tickets/all` (Agent/Admin)
- `GET /api/tickets/{id}/details`
- `POST /api/tickets`
- `POST /api/tickets/{id}/comments`
- `POST /api/tickets/{id}/assign` (Agent/Admin)
- `POST /api/tickets/{id}/status` (Agent/Admin)
- `PATCH /api/tickets/{id}/move`
- `POST /api/tickets/reorder`
- `GET /api/columns`
- `POST /api/columns`
- `DELETE /api/columns/{id}`
- `GET /api/dashboard/summary`
- `GET /api/dashboard/agents` (Agent/Admin)
- `GET /api/users` (Admin)

## Configurazione
Configurazione sviluppo API in `MiniServiceDesk/MiniServiceDesk.Api/appsettings.Development.json`:
- `ConnectionStrings:Default`
- `Jwt:Issuer`
- `Jwt:Audience`
- `Jwt:Key`

Configurazione frontend in `MiniServiceDesk/MiniServiceDesk.Web/appsettings.Development.json`:
- `Api:BaseUrl`

## Build
```bash
dotnet build MiniServiceDesk/MiniServiceDesk.sln
```
