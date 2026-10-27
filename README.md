<p align="center"><img src="assets/banner-sd07.svg" alt="Tech Intuition — System Design Notes" width="100%"></p>

# Tech Intuition · System Design Notes

Interview-grade written walkthroughs that go with the **Tech Intuition** system design videos.

The video builds the **intuition**. These notes give you the **rigour**: requirements, numbers, APIs, schemas, diagrams, failure modes and the follow-up questions interviewers actually ask — written so you can rehearse a clear answer for a big-tech system design round.

## How to use these notes
1. Watch the video once, for the picture in your head.
2. Read the **60-second answer** at the top of the note. Say it out loud.
3. Work through the deep dives; try the **follow-up questions** with the answer folded.
4. Re-read the **Common mistakes** list the night before.

## <a id="series"></a>Problems

| # | Problem | Note | Core ideas |
|---|---|---|---|
| 07 | **Design a ticket booking system** (BookMyShow / Ticketmaster style) | [Read →](sd-07-ticket-booking/README.md) | virtual waiting room · atomic seat holds · UNIQUE as final judge · idempotent payments · outbox · hot partitions |

*Episodes 01–06 are being backfilled in the same format.*

## What every note contains
- ⚡ A **60-second answer**
- Requirements, **back-of-envelope numbers**, API, **data model (real schemas)**
- A **high-level design** diagram, then **deep dives**, each with a sequence/state diagram
- A **consistency map**, **failure-modes cheat-sheet** and **observability**
- **Follow-up questions** with answers, **common mistakes**, and a **45-minute plan**

> [!NOTE]
> The designs are *how you would build it*. When a real company has not published its architecture, these notes say so and never claim internals.

## Contributing
Spotted a mistake or have a better answer to a follow-up? Open an issue or a PR.

<sub>Diagrams are Mermaid, rendered natively by GitHub.</sub>
