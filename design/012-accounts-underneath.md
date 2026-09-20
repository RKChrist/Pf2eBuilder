# 012 — Accounts underneath, not in front

**Status**: accepted
**Follows 006, which promised this.**

A table is a six-letter code, and whoever holds the DM key runs the fight. Design 006 chose that
deliberately and said what it was: a shared secret, honest about being one, proportionate to a
table of friends. It also said phase 3 would put accounts underneath it without changing the
projection.

This is that step, and the first decision is what it does not do.

## Signing in changes nothing about playing

Every route a table uses stays open. A campaign is still a code, the DM is still whoever presents
the key, and no endpoint gained an authorization check. Somebody who never signs in can create a
campaign, import a character, run a fight and end it, exactly as before.

The alternative was to make an account the way in and the campaign code a thing you hold once you
are signed in. It is where this ends up eventually. It is the wrong first move, because it would
have meant changing every one of the fifteen campaign routes, the projection, seven browser
verifiers and the join screen in the same breath as introducing password hashing. Two risky
changes at once, and the rollback is all of it.

So accounts arrive as a foundation with one visible consequence: the shell can say who you are.
What that foundation makes possible comes next, and is listed at the end.

## The token lives in the cookie

The plan fixed this and it is worth restating why it is not belt and braces.

The cookie is an encrypted authentication ticket. The JWT rides inside that ticket. The browser
therefore never holds a readable token: no `localStorage`, nothing for a cross-site script to
read, nothing to leak through a screenshot of developer tools. What the browser holds is an
opaque, HttpOnly cookie it cannot read and cannot be tricked into handing over by script.

The token still earns its place, because it is what a bearer credential will be when the hub and
the API start asking for one, and because it carries its own expiry independent of the cookie's.
To stop it being decoration in the meantime, `GET /accounts/me` reads the token back out of the
ticket and validates it — signature, issuer, audience, lifetime, clock skew — and answers from
its `sub` claim rather than from the cookie's identity. A tampered or expired token is a 401 even
when the cookie itself is still perfectly valid.

That is also the round trip the plan's "green when" asks for, and it is what
`tools/ui-check/verify-account.mjs` drives.

## One `Auth` section, because two cannot be checked against each other

The plan lists `Auth:Cookie` and `Auth:Jwt` as separate sections. They are separate concerns and
they are written that way in `appsettings.json`. They are bound into one options object anyway.

A cookie that outlives the token inside it produces the worst failure this pairing has: a browser
that looks signed in, on every screen, and is refused on every call. Catching that means
comparing `Auth:Cookie:ExpireMinutes` against `Auth:Jwt:AccessTokenMinutes`, and a validator can
only compare two settings when one object holds both.

Validation is an `IValidateOptions<AuthOptions>` rather than data annotations alone. The
requirement is that a missing key fails the boot *naming its section*, and an annotation names a
property. The messages name the configuration path, so the sentence in the console is the line to
go and fix.

## Development makes up its own signing key

Secrets never sit in a checked-in `appsettings.json`. Held strictly, that means the API refuses to
start until every developer has set a user secret, and `run.cmd` stops working for anyone who has
just cloned the repository.

So the key is required, except that Development generates a random one at boot and logs, at
Warning, that it did and that sessions will not survive a restart. Production refuses, and
`verify-boot.mjs` proves it refuses by running the API with `ASPNETCORE_ENVIRONMENT=Production`
and no key, and reading the section name back out of the output.

The risk this accepts is a development machine running with an ephemeral key. The risk it removes
is a repository whose first run fails for a reason nobody can guess.

## Email is an identifier, name is a name

`Email` is stored trimmed and lower-cased and carries the unique index. `DisplayName` is the
human form and is what the shell prints. An account matched case-sensitively on an email address
is a support ticket from somebody whose phone capitalised the first letter.

Sign-in answers the same sentence for an unknown email and for a wrong password. Two different
messages are an oracle that tells an attacker which addresses have accounts here, and a test
asserts the two strings are identical rather than merely that both fail.

## What this makes possible, and is deliberately not here

- A GM's list of their own campaigns, and recovering a DM key on a second device. Today a lost
  key is a lost campaign, and the only reason a reload survives is that the browser remembers it.
- A player claiming a character rather than a browser holding one.
- External providers, which the `Auth:Providers` section is shaped for and nothing reads yet.

Each of those changes what a campaign *is*, so each gets its own step rather than riding along
with password hashing.

## What would make us revisit this

A table that is not a table of friends. The moment somebody runs a public game with strangers
holding the code, the shared secret stops being proportionate and the campaign needs an owner and
an invitation, which is the first item above rather than a change to this record.
