# .githooks

Opt-in git hooks. Enable once per clone:

```bash
git config core.hooksPath .githooks
```

| Hook | What it does | Cost |
|---|---|---|
| `commit-msg` | Rejects a message release-please cannot parse — a bad conventional subject, or a body line that breaks the parser and silently drops the whole commit from the changelog. | instant |
| `pre-commit` | Runs fast, path-filtered checks for the stacks the commit touches. | budget ~2s |

Skip once with `git commit --no-verify`, or set `SKIP_HOOKS=1`.

## Why opt-in and skippable

A hook nobody can bypass gets bypassed anyway — by disabling `core.hooksPath`,
or by not setting it in the first place. Making the escape hatch obvious keeps
the hook installed, which is the only state in which it catches anything.

CI is the authority for correctness. These exist to shorten the loop, not to
replace it.
