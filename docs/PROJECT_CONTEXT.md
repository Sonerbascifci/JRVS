# Project Context

## Vision

Build a practical Windows personal assistant inspired by the interaction model of fictional assistants such as JARVIS, while remaining technically realistic, safe and maintainable.

The assistant should eventually be able to:

- converse naturally in Turkish,
- operate selected desktop applications,
- inspect system status,
- assist with software-development workflows,
- use local memory,
- understand the screen on explicit request,
- integrate with smart-home systems,
- expose a mobile remote client.

## Current priority

The current priority is **not feature breadth**.

It is to establish a foundation that allows each future capability to be added safely.

## Cost constraint

Recurring runtime cost should be kept as close to zero as possible.

Therefore:
- local inference is the default,
- no mandatory API subscription,
- no mandatory hosted database,
- no mandatory cloud infrastructure.

## Product personality

Desired assistant behavior:

- calm,
- concise,
- intelligent,
- professional,
- slightly witty,
- useful rather than chatty,
- action-oriented,
- transparent about failures.

The assistant must not claim actions were completed until tool execution confirms success.

## Initial user language

Primary interaction language:
- Turkish.

Implementation/documentation/code identifiers:
- English.

## Development approach

Use backlog-driven incremental development.

Do not jump directly into voice, vision or automation before the core contracts and safety model exist.
