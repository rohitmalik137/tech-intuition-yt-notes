# Problem bank: what gets asked, and what each one teaches

Pick problems by **building block**, not by company. If you can do one from each shape, you can improvise the rest.

Status: ✅ written · 🎬 video exists · 📝 planned.

## HLD problems

| # | Problem | Shape | Core ideas to master | Status |
|---|---|---|---|---|
| 01 | **Rate limiter** | Coordination | Token bucket, Redis counters, distributed limits | 🎬 note 📝 |
| 02 | **Notification system** | Write-heavy / async | Queues, retries, idempotent consumers, templating | 🎬 note 📝 |
| 03 | **Sharding a big database** | Scale | Shard keys, hot shards, resharding, consistent hashing | 🎬 note 📝 |
| 04 | **Never lose a message** | Messaging | Acks, durability, replication, delivery guarantees | 🎬 note 📝 |
| 05 | **File sync (Dropbox)** | Large objects | Chunking, dedupe, sync protocol, conflict handling | 🎬 note 📝 |
| 06 | **Real-time chat (WhatsApp)** | Real-time | WebSockets, fan-out, presence, ordering, offline delivery | 🎬 note 📝 |
| **07** | **Ticket booking (BookMyShow / Ticketmaster)** | **Contention** | **Waiting room, atomic hold, UNIQUE, idempotency, outbox** | ✅ 🎬 |
| 08 | **URL shortener** | Read-heavy | ID generation (base62), cache, redirect latency, analytics | 📝 |
| 09 | **News feed / timeline** | Read-heavy + fan-out | Push vs pull fan-out, ranking, celebrity problem | 📝 |
| 10 | **Distributed key-value store** (Redis/Dynamo-like) | Storage | Partitioning, replication, quorum, gossip, hinted handoff, LSM | 📝 |
| 11 | **Distributed cache** | Storage | Eviction, consistent hashing, hot keys, invalidation | 📝 |
| 12 | **Video streaming (YouTube/Netflix)** | Large objects | Upload pipeline, transcoding, adaptive bitrate, CDN | 📝 |
| 13 | **Ride hailing (Uber)** | Real-time + geo | Geospatial index (geohash/quadtree), matching, ETA | 📝 |
| 14 | **Search typeahead / autocomplete** | Search | Trie, top-K, caching, offline aggregation | 📝 |
| 15 | **Web crawler** | Throughput | Frontier queue, politeness, dedupe (Bloom), re-crawl | 📝 |
| 16 | **Payment system** | Contention + money | Idempotency, ledger, reconciliation, double-entry | 📝 |
| 17 | **E-commerce checkout / flash sale** | Contention | Inventory reservation, saga, oversell prevention | 📝 |
| 18 | **Distributed job scheduler** | Coordination | Leases, exactly-once-ish execution, retries, priorities | 📝 |
| 19 | **Unique ID generator** | Coordination | Snowflake, clock skew, ordering | 📝 |
| 20 | **Metrics / logging pipeline** | Write-heavy | Ingest, stream processing, time-series, retention | 📝 |
| 21 | **Collaborative editor (Docs)** | Real-time | OT/CRDT, presence, conflict resolution | 📝 |
| 22 | **Maps / proximity service (Yelp)** | Geo | Geohash, quadtree, read-heavy, ranking | 📝 |
| 23 | **API gateway / load balancer** | Infra | Routing, health checks, auth, rate limit | 📝 |
| 24 | **Social graph / "people you may know"** | Graph | Adjacency storage, BFS, offline compute | 📝 |

### Prepare in this order (highest return first)
1. **Ticket booking (07)**, **payment (16)**, **flash sale (17)**: contention and money; they test your depth on correctness.
2. **URL shortener (08)**, **feed (09)**, **cache (11)**: the most common read-heavy fundamentals.
3. **Chat (06)**, **notification (02)**, **messaging (04)**: queues, delivery guarantees, real-time.
4. **KV store (10)**: a frequently reported Microsoft-style prompt; touches replication, partitioning, consistency.
5. The rest, as time allows.

## LLD problems

| Problem | Key ideas | Status |
|---|---|---|
| **Movie ticket booking** | State, Strategy, Observer, atomic multi-seat hold | ✅ [LLD note](sd-07-ticket-booking/LLD.md) |
| Parking lot | Strategy (fee/spot), Factory, concurrent entry | 📝 |
| Vending machine | State machine | 📝 |
| Elevator system | State, Command, scheduling strategy | 📝 |
| LRU cache | HashMap + doubly linked list, thread safety | 📝 |
| Splitwise | Strategy for splits, debt simplification | 📝 |
| Rate limiter (class design) | Strategy for algorithms, injected clock | 📝 |
| Library management | Observer, fines, reservations | 📝 |
| Chess / tic-tac-toe | Polymorphism, move validation, undo | 📝 |
| Notification service | Strategy per channel, retries | 📝 |

## Self-check: could you do these tomorrow?

- [ ] Explain read-heavy vs contention problems and pick tools for each.
- [ ] Do a 60-second capacity estimate on any problem.
- [ ] Explain idempotency and the outbox from memory.
- [ ] Explain sharding strategies and a hot-partition fix.
- [ ] Code a thread-safe class with a state pattern in C#.
- [ ] Say how an in-process lock becomes a distributed one.
