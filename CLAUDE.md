## Guiding Philosophy

1. **Clarify → Offer → Decide ("C‑O‑D" loop)**

   - _Clarify_: If uncertain about requirements, ask a short, pointed question.
   - _Offer_: Propose 2–3 design options when it makes sense to discuss the approach (complex features, architectural decisions).
   - _Decide_: Implement after confirmation, or proceed directly for obvious tasks.

2. **Small, Self‑Contained Units**

   - Functions ≤ 75 LOC, classes ≤ 400 LOC, modules ≤ 600 LOC.
   - Split when you need to scroll – separation of concerns beats DRY‑ness when they conflict.
   - Be pragmatic when facing existing code. We will not refactor anything currently, but you can still apply the principles to new code.

3. **No Hand‑Waving** – Never leave a `# TODO` explaining what "a full solution" _would_ do. Either:

   - implement the slice that is testable today, _or_
   - raise `NotImplementedError("explain_reason")` and create a follow‑up task.

4. **Communicate Uncertainty Early**

   - Preface uncertain statements with _"I'm not sure"_ and immediately ask.
   - Err on the side of over‑communicating assumptions.

5. **Do not overengineer the project**

   - unless told to, to not make the project overly complicated and do not take scalability into the consideration

## Documentation

- [CONTEXT.md](./CONTEXT.md) is the domain language. Read it before naming anything, and
  update it when the language changes.
- [docs/adr/](./docs/adr/) holds decisions and why they were made. A decision that closes off
  an alternative belongs here.

## Workflow

Work reaches `main` through a pull request — `main` is the default branch, and it is not
protected, so nothing stops a direct push except this instruction.

CI (`.github/workflows/ci.yml`) builds and tests the solution and lints the client on every
pull request. Wait for it, then merge on green. Never merge on red, and never merge while
checks are still running. Nothing merges by itself: no auto-merge is configured, so merging
is always a deliberate step.

## Deployment

There is none. Hosting is local only — the poller runs when this machine runs. Do not add
deploy steps, hosting config, or cloud provider setup unless asked.
