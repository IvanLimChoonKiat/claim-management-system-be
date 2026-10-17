# REST API Contract

Base URL: `http(s)://<host>/api`. All endpoints except `/auth/dev-login` require `Authorization: Bearer <token>`. Errors are RFC7807 `ProblemDetails` (`{ status, title, detail, instance }`).

## Auth

### `POST /auth/dev-login`
Dev-only mock login — issues a JWT for whatever email/role is claimed, creating the user on first use. **Not production authentication.**

Request:
```json
{ "email": "alice@example.com", "displayName": "Alice", "role": "Claimant" }
```
`role` is `"Claimant"` or `"ClaimsOfficer"`.

Response `200`:
```json
{
  "token": "<jwt>",
  "user": { "id": "guid", "email": "alice@example.com", "displayName": "Alice", "role": "Claimant" }
}
```

## Claims

### `POST /claims` — submit a claim *(Claimant)*
```json
{
  "type": "Motor",
  "incidentDateUtc": "2026-10-01T00:00:00Z",
  "incidentDescription": "Rear-ended at a traffic light",
  "incidentLocation": "Orchard Road, Singapore",
  "vehicleRegistration": "SGX1234A",
  "propertyAddress": null,
  "estimatedLiability": 5000
}
```
`type` is `"Motor"` or `"Property"`; `vehicleRegistration` required for Motor, `propertyAddress` required for Property. Returns `201` with a `ClaimDetailDto` (includes the generated `claimReference`, e.g. `CLM-2026-A1B2C3D4`).

### `GET /claims/mine` — own claims *(Claimant)*
Returns `ClaimSummaryDto[]`.

### `GET /claims/unassigned` — intake queue *(ClaimsOfficer)*
Returns `ClaimSummaryDto[]` for claims in `Submitted` status.

### `GET /claims/{id}` — claim detail
Claimant: only their own claim (403 otherwise). Officer: any claim. Returns `ClaimDetailDto` (full status history, information requests, decision if any).

### `POST /claims/{id}/assign` *(ClaimsOfficer)*
Assigns the claim to the calling officer. `Submitted → Assigned`. `409` if not currently `Submitted` (including a concurrent-assignment race).

### `POST /claims/{id}/information-requests` *(ClaimsOfficer, must be the assigned officer)*
```json
{ "requestText": "Please provide the police report number" }
```
`Assigned → InfoRequested`. `403` if caller isn't the assigned officer; `409` if not currently `Assigned`.

### `POST /claims/{id}/information-requests/{requestId}/respond` *(Claimant, must own the claim)*
```json
{ "responseText": "Police report number PR-998877" }
```
`InfoRequested → Assigned`. `403` if caller doesn't own the claim; `409` if not currently `InfoRequested`.

### `POST /claims/{id}/decision` *(ClaimsOfficer, must be the assigned officer)*
```json
{ "outcome": "Approved", "settlementAmount": 4500, "reason": "Valid claim, police report confirms liability" }
```
`outcome` is `"Approved"` (requires a positive `settlementAmount`) or `"Rejected"` (`settlementAmount` omitted). `Assigned → Approved|Rejected` (terminal). `403`/`409` as above.

## Officers

### `GET /officers/me/claims` — workload *(ClaimsOfficer)*
Returns `ClaimSummaryDto[]` for claims currently assigned to the calling officer.

## Dashboard

### `GET /dashboard/summary` — team-wide operational visibility *(ClaimsOfficer)*
Open to any Claims Officer — the brief defines Claimant and ClaimsOfficer as the only two roles; "Manager" appears only in the background narrative, not as a distinct login, so this is not gated behind a separate role (see `docs/ASSUMPTIONS.md`).

```json
{
  "totalOpenClaims": 7,
  "outstandingLiability": 23500.00,
  "claimsByStatus": [
    { "status": "Submitted", "count": 2 },
    { "status": "Assigned", "count": 3 },
    { "status": "InfoRequested", "count": 2 },
    { "status": "Approved", "count": 5 },
    { "status": "Rejected", "count": 1 }
  ],
  "officerWorkloads": [
    { "officerId": "guid", "officerName": "Bob Officer", "activeCount": 3, "approvedCount": 4, "rejectedCount": 1, "totalHandled": 5 }
  ]
}
```

`outstandingLiability` sums `estimatedLiability` across claims still in `Submitted`/`Assigned`/`InfoRequested` (not yet resolved) — an approved/rejected claim's estimated liability no longer counts as open exposure. `officerWorkloads.activeCount` is the officer's current open assignments ("team workload"); `approvedCount`/`rejectedCount`/`totalHandled` are the simplest honest read of "performance" the brief's scope supports (decision counts, not cycle-time/SLA metrics).

## Status codes

| Code | Meaning |
|---|---|
| 200 / 201 | Success |
| 400 | Request validation failure (missing/invalid field) or domain validation (e.g. motor claim missing vehicle registration) |
| 401 | Missing/invalid token |
| 403 | Authenticated, but wrong role or not the claim's owner/assigned officer |
| 404 | Claim not found |
| 409 | Invalid status transition, or concurrent-update conflict |
| 500 | Unexpected error (detail hidden outside Development) |
