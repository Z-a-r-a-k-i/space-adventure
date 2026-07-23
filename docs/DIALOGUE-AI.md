# Controlled dialogue AI

## Intent

Future generated dialogue should make NPCs responsive without allowing a model to improvise the rules, history, inventory, objectives, or outcome of the universe.

Dialogue is therefore a versioned game protocol, not an open-ended agent with access to the world. The game selects relevant facts and allowed moves; a provider proposes wording and bounded intent; deterministic validators decide what becomes authoritative.

The POC uses authored dialogue. Generated dialogue is a separately gated experiment and is never required to finish the first demo.

## Development access through the ChatGPT subscription

A ChatGPT subscription is not general API access, but Codex officially supports ChatGPT sign-in and a non-interactive CLI. For this private development POC, a local provider may invoke `codex exec` using the developer's signed-in subscription, receive a schema-constrained response, and pass it through the normal dialogue validator. This avoids an API key without scraping ChatGPT, browser automation, or unofficial session endpoints.

The automated development flow is:

1. The game or dialogue lab exports a self-contained request packet.
2. A local bridge starts an isolated, non-interactive Codex request with the selected experiment profile.
3. Codex returns the requested structured response envelope.
4. The game-side validator accepts or rejects it.
5. Accepted turns are recorded and become deterministic fixtures for subsequent playtests.

A manual inbox remains useful for prompt inspection and recovery, and the scripted provider remains the POC default. Saved Codex authentication is local developer tooling, never game content or a distributable player credential. If generated dialogue is ever shipped to other players, it will require a supported production provider and its own deployment, privacy, quota, and cost design.

## Provider boundary

All providers implement the conceptual operation:

```text
Generate(DialogueRequest) -> DialogueProviderResult
```

Expected providers, introduced only when needed:

- `ScriptedDialogueProvider`: authored POC dialogue and deterministic tests.
- `CodexCliDialogueProvider`: optional local personal-play experiment using official ChatGPT sign-in.
- `ManualInboxDialogueProvider`: imports manually supplied candidate responses.
- `RecordedDialogueProvider`: replays previously accepted turns.
- A future local or hosted provider behind the same contract.

Provider failures return a typed result. They do not corrupt conversation state or strand the player; authored fallback text or choices remain available where progression depends on the conversation.

## Context construction

The game builds the smallest sufficient context projection rather than sending the universe. A versioned request may include:

- Request, schema, prompt, world, content, scenario, and conversation revisions.
- NPC identity, role, voice constraints, current state, and relationship band.
- Participants and visible situation.
- Fact IDs the speaker is allowed to know.
- A bounded summary of recent accepted turns.
- The player's selected authored response or free-text utterance.
- Allowed intents and permitted state moves for this exact turn.
- Output size, tone, safety, and latency constraints.

Stable fact IDs carry truth. Natural-language lore summaries explain those facts to the model but are not themselves authoritative evidence.

A free-text utterance is the protagonist's literal speech. The provider may generate the NPC's response but must not rewrite the player's line. Authored response suggestions remain available for players who prefer a concise, voice-consistent choice.

## Response envelope

A candidate response echoes the request identity and revisions, then proposes:

- Spoken text.
- Intent and tone from allowed enumerations.
- Referenced fact IDs.
- Zero or more bounded moves.
- Optional developer diagnostics kept out of player-visible text.

Examples of bounded moves include revealing an allowed fact, offering an authored objective, selecting an authored route response, changing attitude within a permitted range, or ending the conversation. Free prose cannot create an item, objective, person, location, relationship value, or world fact.

Exact DTOs and JSON Schema belong with the implementation and are generated or tested from one source. This document defines policy rather than duplicating field-level code contracts.

## Validation

Validation occurs before any accepted line or effect becomes game state:

1. Parse the exact schema and enforce byte, token, list, and text limits.
2. Match request identity, revisions, NPC, participants, and turn number.
3. Reject unknown facts, entities, intents, tones, or moves.
4. Verify that referenced facts are available to the speaker.
5. Check scenario, relationship, safety, and state-transition preconditions.
6. Sanitize presentation text without changing its semantic proposal.
7. Apply permitted effects through normal authoritative gameplay commands.
8. Record the request, raw candidate, validation result, accepted response, provider metadata, and resulting events.

Player text and model text are untrusted content. Neither can grant tools, alter instructions, enlarge permissions, or supply authoritative facts.

## Coherence strategy

Coherence comes from layered authored constraints:

- A versioned world bible and fact registry.
- Character identity, knowledge, goals, voice, and prohibited claims.
- Scenario-local state and allowed transitions.
- Retrieval of explicit fact IDs rather than unrestricted lore generation.
- Bounded recent-turn summaries.
- Validation and authored fallbacks.
- Golden conversation scenarios that exercise contradictions, manipulation attempts, missing knowledge, and long context.

The model expresses a permitted response; it does not simulate the entire NPC or decide the plot.

## Runtime-selectable experiment profiles

Dialogue quality is the primary objective. Latency is measured and constrained, but we do not choose a faster setup when it makes replies less coherent or relevant.

The developer-only dialogue panel exposes three independent Codex controls:

- Model: `gpt-5.6-sol`, `gpt-5.6-terra`, or `gpt-5.6-luna` when available to the signed-in account.
- Reasoning effort: the levels supported by the selected model and Codex client, including low, medium, high, and any available advanced levels.
- Fast mode: an explicit checkbox, off by default, independent of the selected model and reasoning effort.

The initial profile is Sol, medium reasoning, Fast mode off. This is an experiment baseline, not a permanent production choice. Unsupported combinations are disabled rather than silently substituted.

These controls can be changed without rebuilding the game. They are available from a developer panel and the dialogue-lab command line, with a versioned configuration default for unattended scenarios. A change applies to the next request; a request already in flight retains the profile with which it started.

Each request record includes the requested profile ID, resolved provider, exact model, reasoning effort, Fast-mode state, prompt revision, Codex version, start time, first-response time when observable, completion time, validation result, and fallback used. Provider settings never enter authoritative world or save state.

## Quality and latency evaluation

Do not select a model from reputation alone. Run candidate model × reasoning × Fast-mode combinations against the same committed scenario set and score, in priority order:

- Valid-envelope rate.
- Fact support and contradiction rate.
- Relevance to the player's actual utterance and current situation.
- Character voice and conversational continuity.
- Valid proposed-move rate.
- Median and tail latency, including time to first visible text where available.
- Context size and credit or operating cost where applicable.

The same recorded request packets make comparisons fair. Human review remains decisive for coherence and relevance; automatic checks catch schema failures, unsupported facts, forbidden moves, and obvious continuity errors. A faster profile is adopted only when its quality remains acceptable for its intended class of conversation.

Exact repeatability cannot be expected from a changing hosted model. Accepted responses are recorded for replays and regression tests.

## Not allowed

- Browser automation that extracts ChatGPT responses.
- Shipping the developer's ChatGPT authentication or requiring players to use it.
- Direct model access to save files, commands, editor tools, or arbitrary retrieval.
- Unvalidated free-text effects.
- Generated critical plot facts with no authored fact IDs.
- Silent fallback that changes the meaning of a choice.
- A network or consumer-session dependency in the POC critical path.
- Provider-specific data inside authoritative gameplay state or save data.
