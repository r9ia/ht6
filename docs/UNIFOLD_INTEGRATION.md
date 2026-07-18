# Unifold Integration Contract

## Verified sponsor-track requirements

Source: `Best Use of Unifold — Sponsored Track.pdf`, supplied locally on 2026-07-17.

- Prize: **$1,000 USD**.
- Eligibility: any hackathon project—web, mobile, or game—that integrates the **Unifold SDK**.
- Required capability: use Unifold to power a stablecoin **deposit or payment** flow.
- Explicitly accepted project type: a game with on-chain rewards.
- Documented SDK languages: **TypeScript, React Native, Swift, Kotlin**.
- Sponsor support: password-protected documentation, API keys, sandbox access, and Discord support are to be provided.
- Judging: integration centrality, creativity, end-to-end execution/polish including error handling, and technical quality/multi-chain handling where relevant.

## Product decision

Dread Director will make Unifold a visible, functional game companion rather than an isolated “claim reward” button.

The proposed product surface is **Night Watch Contract**:

1. Unity emits an opaque `game_session_id` when a player reaches the relevant game milestone.
2. Unity opens a lightweight TypeScript/web companion owned by the project (`unifold-bridge/`).
3. The bridge uses the real Unifold SDK to present one sponsor-supported sandbox stablecoin payment/deposit flow.
4. On a completed flow, Unity shows the associated in-game achievement/reward state. On cancellation/error, Unity gives clear feedback and leaves core gameplay unaffected.

The exact user-facing narrative, payment/deposit direction, supported chain, amount, wallet requirements, and reward semantics must be selected **after** reviewing the private Unifold docs and sandbox. Do not build a bespoke wallet, settlement layer, or contract before then.

## Integration boundary

```text
Unity game ──opaque game_session_id──► TypeScript Unifold bridge
     ▲                                       │
     └──completed/pending/cancelled/failed───┘
                                             │
                                      Unifold SDK sandbox flow
```

### Unity responsibilities

- Display the achievement and explain the optional sponsor flow.
- Launch the companion checkout/deposit UI or invoke its local bridge endpoint.
- Receive normalized status only: `completed`, `pending`, `cancelled`, `failed`.
- Provide clear loading, success, error, retry, and cancel screens.
- Never store Unifold secrets or invoke undocumented SDK APIs.

### TypeScript bridge responsibilities

- Be the only module that imports the Unifold SDK.
- Follow the supplied Unifold SDK’s client/server/API-key model exactly.
- Validate game-session input and map provider callbacks to normalized status.
- Use Unifold multi-chain support where applicable; do not replicate wallet/chain/settlement logic.
- Log no secret material or biometric payloads.

### QNX and biometrics responsibilities

- QNX has no Unifold, wallet, or Internet dependency.
- No raw or derived biometric data crosses into the bridge, Unifold, a wallet, or a blockchain.
- The only permitted cross-boundary game value is a random/opaque game session or achievement identifier.

## Definition of done

The Unifold feature is demo-ready only when all are true:

- [ ] The actual sponsor-provided Unifold SDK is imported in the TypeScript bridge.
- [ ] A sandbox stablecoin deposit/payment completes successfully end to end.
- [ ] Unity receives and displays the completed status.
- [ ] Cancellation and a deliberately induced error show understandable UI without breaking the game.
- [ ] The demo can explain why Unifold is central to the Night Watch Contract flow.
- [ ] No secrets, wallet exports, signed transactions, participant identifiers, or biometric data are committed or transmitted to Unifold.
- [ ] Any claim of Solana support has been verified in Unifold’s supplied sandbox/docs.

## First action when sponsor access arrives

1. Read the Unifold docs and supported-chain/payment-method matrix.
2. Confirm the TypeScript package name, installation version, required environment variables, callback/webhook model, and sandbox network.
3. Ask the Unifold Discord contact which game-payment/reward pattern best demonstrates deposits/payments without custom settlement logic.
4. Replace this design contract’s placeholders with verified API details before implementation.

## Verified implementation (`unifold-bridge/`)

The placeholders above are now resolved. Verified from Unifold's project-scoped docs (`llms-full.txt`, `skill.md`) and implemented in `unifold-bridge/`.

### Selected product surface

**Night Watch Contract — survival + composure bounty.** Surviving longer and staying calmer (being less scared) earns a sandbox stablecoin (USDC) reward. Reward direction is a **payout** (an accepted "game with on-chain rewards" pattern): the bridge issues a Unifold **treasury outbound transfer** to a demo recipient, scaled by an opaque achievement tier. Unity folds local survival time and composure into one tier and sends only `{ version, claimId, tier }`.

### Verified API details

- **SDK / transport:** TypeScript `@unifold/node` (secret-key, server-side), used SDK-first via a runtime-assembled dynamic import; falls back to the documented REST endpoint `POST /v1/treasury/outbound_transfers`. Both use the `claimId` as the idempotency key (required by Unifold).
- **Auth:** secret key `sk_...` in the ignored root `.env`; never client-side, never committed.
- **Environment variables:** see `.env.example` (`ENABLE_UNIFOLD_REWARDS`, `UNIFOLD_SECRET_KEY`, `UNIFOLD_TREASURY_ACCOUNT_ID`, `UNIFOLD_DEMO_RECIPIENT_ADDRESS`, chain/token settings).
- **Chain/token:** default **Base (`8453`) USDC** (`0x833589fcd6edb6e08f4c7c32d4f71b54bda02913`, 6 decimals). Treasury outbound transfers support Polygon `137`, Base `8453`, and Solana `mainnet`.
- **Solana note:** Solana treasury sources require `source.chain_id: "mainnet"` explicitly. Default demo uses Base, so no Solana support is claimed unless that treasury/chain is configured.
- **Sandbox/mock:** the bridge defaults to a network-free mock payout, preserving the replay/fake path. Live payouts require the opt-in flag plus a secret key, treasury id, and recipient.
- **Status model:** provider statuses are normalized to `completed | pending | cancelled | failed` for Unity.

### Definition of done — status

- [x] The bridge is the only module that touches Unifold; it imports the real `@unifold/node` SDK (SDK-first) with a REST fallback.
- [x] Unity receives and displays normalized status (`UnifoldRewardBridgeClient`).
- [x] Errors and unavailability show understandable UI and never break gameplay (offline/mock fallback, loopback guard).
- [x] No secrets, wallet exports, signed transactions, participant identifiers, or biometric data are committed or transmitted to Unifold (only an opaque `claimId` + tier cross; `claimId` is reused as `external_user_id`).
- [ ] A sandbox stablecoin payout completes end to end against live Unifold sandbox — pending real sandbox keys, a funded test treasury, and a recipient in `.env` (mock path verified offline; live path implemented but not yet exercised against the sandbox).
- [ ] Any Solana support claim verified in the sandbox — not claimed by default (Base USDC).
