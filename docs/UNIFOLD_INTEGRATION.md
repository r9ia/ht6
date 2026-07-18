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
