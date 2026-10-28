# A framework for any HLD question

You will not have seen the exact problem. A repeatable framework is what stops you freezing.

## The 7 steps

| # | Step | Time (of 45 min) | What to say |
|---|---|---|---|
| 1 | **Clarify requirements** | 5 | "Let me confirm scope: who uses it, what must it do, and what's out of scope?" |
| 2 | **Non-functional requirements & numbers** | 4 | Latency, availability, consistency, scale. Do back-of-envelope maths. |
| 3 | **API** | 3 | Sketch 4–6 endpoints; mark which are idempotent. |
| 4 | **Data model** | 4 | Entities, keys, the access patterns that decide the store. |
| 5 | **High-level design** | 8 | Draw boxes and flows for the main path. |
| 6 | **Deep dives** | 16 | Pick the **2–3 hardest parts**. This is where you are scored. |
| 7 | **Wrap-up** | 5 | Failure modes, bottlenecks, monitoring, trade-offs, what you would do next. |

## Step 1: questions worth asking

- Who are the users, and how many? Read-heavy or write-heavy?
- Must it be **strongly consistent** or is eventual fine? (This choice drives everything.)
- What is the **latency** target for the main operation?
- Is traffic **steady or spiky**? Is the spike predictable (a sale) or not (viral)?
- What is explicitly **out of scope**? (Say it. It shows judgement.)

## Step 2: classify the problem

Most questions fall into a few shapes. Naming the shape tells you which building blocks to reach for.

| Shape | Signal | Reach for |
|---|---|---|
| **Read-heavy** | "Millions read, few write" (feeds, URL shortener, product pages) | CDN, cache, read replicas, denormalise |
| **Write-heavy** | Logs, metrics, chat, IoT | Queues/streams, append-only stores, partitioning |
| **Contention on scarce inventory** | Tickets, seats, flash sales, bookings | Atomic claim, idempotency, waiting room, strong consistency on the inventory |
| **Real-time** | Chat, presence, live location | WebSockets/long-poll, pub/sub, fan-out |
| **Large objects** | Video, files, images | Object storage, chunking, CDN, async processing |
| **Search / ranking** | Typeahead, search, recommendations | Inverted index, precompute, caching |
| **Coordination** | Locks, leader election, schedulers | Consensus store, leases, fencing tokens |

## Step 6: how to choose the deep dives

Ask: *"What would break this first at 100× load, or on a bad day?"* Examples:

- Two users want the same thing at the same instant → **concurrency control**.
- A message could be delivered twice → **idempotency and dedupe**.
- One key is far hotter than the rest → **hot partitions**.
- A dependency can be down → **retries, timeouts, circuit breakers, graceful degradation**.
- Two stores must agree → **transactions across stores: outbox, saga, reconciliation**.

## Phrases that score well

- "I'll **assume** X; tell me if that's wrong."
- "The trade-off here is A vs B. I'd pick A because…, and it costs us…"
- "That's a **single point of failure**, so I'd add…"
- "To be safe against duplicates, I'd make this operation **idempotent** by…"
- "I would **measure** this: the metric I'd watch is…"

## Mistakes that cost offers

- Jumping to boxes before asking a single question.
- Naming technologies without saying *why* (a name is not a reason).
- Designing for 1 billion users when the question said thousands.
- Never choosing a depth area; staying at the surface for 45 minutes.
- Ignoring failure, duplicates and retries.
- Not stating consistency needs.
- Silence. Think out loud.
