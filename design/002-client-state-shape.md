# 002. Client state shape

Decided 19 September 2026. Named before any component was written.

## The store

One Fluxor feature per vertical slice. The first slice is browsing rules.

```
RulesBrowserState
    GroupKey        ActiveGroup     which of six groups is showing
    string          ActiveCategory  one of the 74
    string          Query           name filter, debounced
    int?            MinLevel
    int?            MaxLevel
    string?         Trait
    int             Page
    RemoteData      Results         see below
```

## Loading is a state, not two booleans

`Results` is a discriminated union, not `IsLoading` plus `Error` plus `Data`:

```
RemoteData<T> = NotAsked | Loading | Loaded(T) | Failed(string)
```

Three booleans admit eight combinations and only four are legal. "Loading and
failed at the same time" and "loaded with an error" are the bugs that produce a
spinner over a stale list. The union makes them unrepresentable, and the render
is one switch with four arms rather than a chain of guards.

## Categories are a registry, not a menu

74 categories is not a phone picker. They group into six, and the grouping is a
table rather than branching:

| Group | Holds |
| --- | --- |
| Build | ancestry, heritage, background, class, class-feature, and the 31 subclass categories |
| Feats | feat, archetype |
| Spells | spell, ritual, tradition |
| Gear | equipment, weapon, armor, shield, item-bonus, relic, set-relic, curse, class-kit |
| Play | action, condition, skill, skill-general-action |
| Reference | trait, language, deity, deity-category, domain, source, and the rest |

Adding a category is a row. The renderer never learns a category name.

## Why no client-side rules cache

The temptation is to pull a category once and filter in the browser. Feats alone
are 6,390 records. The API already pages and filters, and a phone on table wifi
is better served by a 50-row page than by a 6,390-row download it then filters.
