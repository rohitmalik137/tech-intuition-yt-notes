# Start here — how to use these notes (and how interviews actually work)

If you are new to system design, read this page first. It takes ten minutes and saves weeks.

## 1. What a big-tech loop looks like

Candidate-reported loops (LeetCode, Glassdoor; not official) for Microsoft SDE roles describe roughly this shape. It varies by team, level and country, so treat it as a map, not a promise.

| Round | What it is | Where these notes help |
|---|---|---|
| **Online assessment** | A couple of medium coding problems | (not covered here) |
| **Coding / DSA** | Data structures and algorithms, clean code, edge cases | (not covered here) |
| **LLD** (low-level design) | Design **classes and interfaces** for one component, usually code it (parking lot, vending machine, **movie ticket booking**) | [`fundamentals/04-lld-guide.md`](fundamentals/04-lld-guide.md) and each problem's `LLD.md` |
| **HLD** (high-level design) | Design a **distributed system** (key-value store, Twitter, **ticket booking**), covering scale, storage, trade-offs | [`fundamentals/01-hld-framework.md`](fundamentals/01-hld-framework.md), [`03-building-blocks.md`](fundamentals/03-building-blocks.md) and each problem's `README.md` |
| **Hiring-manager / behavioural** | Past projects, architecture decisions, collaboration | (not covered here) |

Two reported examples are worth knowing: one Redmond SDE 2 candidate described an **HLD for a movie-ticket booking system and then an LLD of the same system**, and another SDE 2 round reported **LLD for a vending machine or parking lot** plus an **HLD for a Redis-like distributed key-value store**.

> [!NOTE]
> **HLD vs LLD.** HLD = *boxes and arrows at scale* (services, databases, queues, caching, sharding). LLD = *classes inside one box* (objects, interfaces, patterns, thread safety). Same product, different zoom level.

**Sources (candidate reports and prep guides, not official):**
[Microsoft SDE 2 experience, LeetCode 2026](https://leetcode.com/discuss/post/7769548/microsoft-interview-experience-sde-2-l61-xdc2/) ·
[Frequently asked LLD questions, LeetCode](https://leetcode.com/discuss/post/5328221/frequently-asked-low-level-design-lld-qu-l0xk/) ·
[Common LLD questions in 2025](https://levelup.gitconnected.com/common-lld-questions-in-2025-5abab30c539f) ·
[Educative: LLD questions for system design roles](https://www.educative.io/blog/low-level-design-interview-questions-for-system-design-roles)

## 2. What interviewers are really scoring

Not whether you memorised a famous architecture. They are scoring:

1. **Do you clarify** before you build? (requirements, scale, what is out of scope)
2. **Do you reason with numbers?** (requests per second, storage, why a design is needed)
3. **Do you find the hard part** and spend your time there?
4. **Do you explain trade-offs** ("I chose X over Y because…")?
5. **Do you handle failure** (crashes, duplicates, retries, hot spots)?
6. **Can you communicate** clearly while drawing?

Every note here is written to train exactly those six habits.

## 3. The learning method (this is what makes it fast)

Reading is the slowest way to learn this. Use this loop for each problem:

1. **Watch the video** (intuition, 10–15 min).
2. **Say the "60-second answer" out loud** without looking. Fix what you forgot.
3. **Read one deep dive**, then **close the page and redraw it** on paper.
4. **Answer the follow-up questions** with the answers folded.
5. **Do the LLD note** with a real editor; type the code, do not just read it.
6. **Next day, redo step 2 cold.** Spaced repetition beats a long cram.

## 4. A 6-week plan (about 8 hours a week)

| Week | Focus | Do |
|---|---|---|
| **1** | Foundations | Read `01-hld-framework`, `02-numbers`, skim `03-building-blocks`. Learn the numbers cold. |
| **2** | Building blocks | Work through `03-building-blocks` (caching, DB, sharding, queues, consistency). Make a one-page cheat sheet in your own words. |
| **3** | First full problem | Do **SD-07 ticket booking** (HLD) end to end, then its **LLD**. Mock it with a friend. |
| **4** | More problems | Next 3 from the [problem bank](problem-bank.md): pick one *read-heavy*, one *write-heavy*, one *real-time*. |
| **5** | LLD depth | Read `04-lld-guide`. Code 3 LLD classics in C# (parking lot, vending machine, LRU cache). |
| **6** | Mocks + gaps | 3 timed mocks (45 min each), record yourself, review against the "common mistakes" lists. |

## 5. How each problem note is organised

Every problem folder follows the same layout so you always know where to look:

```
sd-NN-problem/
  HLD.md        HLD: requirements → numbers → API → data model → design → deep dives → failure modes → follow-ups
  LLD.md        LLD: framework → class diagram → patterns → full C# code → concurrency → extensions
  lld-csharp/   the runnable project
```

## 6. Where to go next

- [`fundamentals/`](fundamentals/) — the reusable knowledge (read these once, reuse everywhere)
- [`problem-bank.md`](problem-bank.md) — what gets asked, with the building blocks each one needs
- [`glossary.md`](glossary.md) — every term, one line each
- [SD-07 HLD](sd-07-ticket-booking/HLD.md) and [SD-07 LLD](sd-07-ticket-booking/LLD.md) — the worked example
