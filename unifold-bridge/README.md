# Unifold Reward Bridge

Local, loopback-only service that powers the **Night Watch Contract**: players who survive longer and stay calmer (less scared) earn a sandbox stablecoin (USDC) bounty. This is the **only** module in the project that talks to Unifold.

## Privacy boundary

Unity measures survival time and composure locally and folds them into one opaque achievement tier. The game sends only:

```json
{ "version": 1, "claimId": "nightwatch_<opaque>", "tier": "composed_survivor" }
```

No biometrics, Director scores, identity, or wallet ever cross into this bridge. The `claimId` is reused as Unifold's `external_user_id`, so Unifold receives no player identity either. QNX has no involvement.

## Setup

```powershell
Copy-Item ..\.env.example ..\.env
# Leave the Unifold values blank to run in mock mode (no keys, no funds, no network).
npm install
npm test
npm start
```

The service binds to `127.0.0.1:8788` by default (the narration bridge uses `8787`).

## Mock vs live

Mock mode is the default and preserves the project-wide replay/fake path — the demo runs end to end with no keys and no network, returning a simulated `completed` payout. It stays in mock mode unless **all** of these are set in the root `.env`:

- `ENABLE_UNIFOLD_REWARDS=true`
- `UNIFOLD_SECRET_KEY=sk_...` (server secret key; never commit it)
- `UNIFOLD_TREASURY_ACCOUNT_ID=ta_...`
- `UNIFOLD_DEMO_RECIPIENT_ADDRESS=0x...` (a demo wallet; a real app uses the player's wallet)

In live mode the bridge issues a Unifold **treasury outbound transfer** of USDC, scaled by tier, with the `claimId` as the idempotency key. It calls the official `@unifold/node` SDK when installed (`npm install @unifold/node@`, using the exact published version and after confirming platform support), and otherwise falls back to the documented REST endpoint `POST /v1/treasury/outbound_transfers`. Treasury payouts support Polygon (`137`), Base (`8453`), and Solana (`mainnet`).

## Reward tiers

Tiers ascend with survival duration and composure. Defaults (USD, capped by `UNIFOLD_MAX_REWARD_USD`):

| Tier | Meaning | Default reward |
| --- | --- | --- |
| `endured` | Survived a meaningful stretch | 0.25 USDC |
| `survivor` | Survived the full night | 0.50 USDC |
| `composed_survivor` | Survived and held composure | 1.00 USDC |
| `unshaken` | Survived a long night, stayed very calm | 2.00 USDC |

## API

```http
GET /health
POST /v1/reward/claim
Content-Type: application/json

{"version":1,"claimId":"nightwatch_smoke123","tier":"unshaken"}
```

`/v1/reward/claim` validates the opaque claim, is idempotent per `claimId` (a replay returns the original result with `reused: true` and never re-pays), and returns a normalized status of `completed`, `pending`, `cancelled`, or `failed`. Unsupported tiers, bad claim ids, oversized bodies, and non-loopback callers are rejected.
