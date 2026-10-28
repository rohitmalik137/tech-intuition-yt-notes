# Building blocks: the vocabulary of every HLD answer

For each block: **what it is → when to use it → the trade-off → the line to say.** Learn these once; every problem reuses them.

## Contents
1. [Load balancer](#1-load-balancer) · 2. [CDN](#2-cdn) · 3. [Caching](#3-caching) · 4. [Choosing a database](#4-choosing-a-database) · 5. [Indexes](#5-indexes) · 6. [Replication](#6-replication) · 7. [Sharding](#7-sharding-partitioning) · 8. [Consistency, CAP, PACELC](#8-consistency-cap-pacelc) · 9. [Queues and streams](#9-queues-and-streams) · 10. [Idempotency](#10-idempotency) · 11. [Transactions across services](#11-transactions-across-services) · 12. [Concurrency control](#12-concurrency-control) · 13. [Rate limiting](#13-rate-limiting) · 14. [API gateway & service patterns](#14-api-gateway-and-service-patterns) · 15. [Reliability patterns](#15-reliability-patterns) · 16. [Object storage](#16-object-storage-and-large-files) · 17. [Search](#17-search) · 18. [Real-time delivery](#18-real-time-delivery) · 19. [ID generation](#19-id-generation) · 20. [Consensus and locks](#20-consensus-leader-election-and-locks) · 21. [Probabilistic structures](#21-probabilistic-data-structures) · 22. [Observability](#22-observability) · 23. [Security basics](#23-security-basics) · 24. [Cloud mapping (Azure)](#24-cloud-mapping-azure-flavoured)

---

## 1. Load balancer
- **What:** spreads requests across servers; removes unhealthy ones via health checks.
- **Layers:** **L4** (TCP, fast, dumb) vs **L7** (HTTP-aware: routing by path/header, TLS termination).
- **Algorithms:** round robin, least connections, weighted, **consistent hashing** (for caches/stickiness).
- **Trade-off:** the LB itself is a single point of failure → run it redundantly (active-active / DNS or anycast in front).
- **Say:** "Stateless services behind an L7 load balancer so any instance can serve any request."

## 2. CDN
- **What:** caches content near users. Static assets, pages, video segments.
- **When:** read-heavy, same answer for many users.
- **Trade-off:** staleness. Control with TTLs and purge/invalidate; versioned URLs for assets.
- **Say:** "Anything cacheable goes to the edge; origin only sees misses."

## 3. Caching
- **Where:** client, CDN, app-local, distributed (Redis/Memcached), DB buffer cache.
- **Patterns**

| Pattern | How | Good for | Risk |
|---|---|---|---|
| **Cache-aside** (lazy) | App reads cache; on miss reads DB and fills cache | Most read-heavy apps | Stale data; cold start |
| **Read-through** | Cache loads from DB itself | Simpler app code | Same |
| **Write-through** | Write cache and DB together | Read-after-write correctness | Slower writes |
| **Write-behind** | Write cache; flush to DB later | Burst writes | Data loss on crash |

- **Eviction:** LRU (default choice), LFU, TTL.
- **Known problems:**
  - **Stampede / thundering herd:** a hot key expires and thousands hit the DB → request coalescing, jittered TTLs, lock-on-miss.
  - **Penetration:** requests for keys that do not exist → cache negative results, Bloom filter.
  - **Hot key:** one key overloads one node → replicate it, local cache, split the key.
  - **Invalidation:** the hard part. Prefer short TTLs or event-driven invalidation.
- **Say:** "Cache-aside with a short TTL, and I'd protect against stampedes with request coalescing."

## 4. Choosing a database
Start from the **access pattern**, not the brand.

| Need | Pick | Why |
|---|---|---|
| Transactions, joins, constraints, moderate scale | **Relational** (SQL Server, Postgres, MySQL) | ACID, UNIQUE/FK constraints, mature |
| Huge scale, simple key access, flexible schema | **Key-value / wide-column** (DynamoDB, Cassandra, Cosmos DB) | Horizontal scale, tunable consistency |
| Document-shaped data, nested | **Document** (MongoDB, Cosmos DB) | Fits aggregate reads |
| Relationships are the query | **Graph** (Neo4j) | Traversals |
| Full-text search | **Search engine** (Elasticsearch) | Inverted index |
| Time-ordered metrics | **Time-series** (InfluxDB, Timescale) | Compression, rollups |
| Blobs | **Object store** (S3, Azure Blob) | Cheap, durable |
| Ephemeral / fast / atomic | **In-memory** (Redis) | Sub-ms, TTL, atomic ops |

- **SQL vs NoSQL is not a religion.** Say the access pattern and the consistency need.
- **Default:** start relational; move a part to NoSQL only when scale or shape demands it.
- **Say:** "I need a uniqueness guarantee on (show, seat), which a relational constraint gives me for free."

## 5. Indexes
- **B-tree** (range + equality, the default), **hash** (equality only), **LSM-tree** (write-heavy stores), **inverted** (text).
- **Cost:** every index speeds reads and slows writes and uses space.
- **Composite index** order matters: put equality columns first, then range.
- **Covering index** answers a query from the index alone.
- **Say:** "I'd index the access path `(user_id, created_at)` for the timeline query."

## 6. Replication
| Model | How | Use | Cost |
|---|---|---|---|
| **Leader–follower** | One writer, many readers | Read scaling, failover | Replication lag → stale reads |
| **Multi-leader** | Several writers | Multi-region writes | Conflict resolution |
| **Leaderless / quorum** | Read/write to N nodes (R + W > N) | High availability | Tunable, eventual |

- **Sync vs async:** sync is safer (no loss), slower; async is faster, can lose the last writes on failover.
- **Read-your-writes:** route a user's reads to the leader briefly after they write.
- **Say:** "Async replicas for read scale; I accept a small lag for browsing but read the leader for the user's own just-written data."

## 7. Sharding (partitioning)
- **Why:** one node cannot hold or serve everything.
- **Strategies**

| Strategy | How | Good | Bad |
|---|---|---|---|
| **Range** | Key ranges per shard | Range scans | Hot ranges (timestamps) |
| **Hash** | `hash(key) mod N` | Even spread | Resharding moves lots of data; no range scans |
| **Consistent hashing** | Keys and nodes on a ring | Adding a node moves ~1/N of keys | Needs virtual nodes for balance |
| **Directory** | Lookup table | Flexible | Lookup is a dependency |

- **Choose the shard key** so the common query hits **one** shard and load is even.
- **Hot partitions:** a celebrity, a hot show section. Split further (sub-shard), add a salt, or cache.
- **Cross-shard transactions are expensive:** design so the invariant lives in **one** shard.
- **Say:** "Shard by show, then section, so the uniqueness check for a seat never spans shards."

## 8. Consistency, CAP, PACELC
- **Strong consistency:** every read sees the latest write. Costs latency/availability.
- **Eventual consistency:** replicas converge given time. Fast and available.
- **CAP:** during a **network partition** you choose **C**onsistency *or* **A**vailability. (You cannot opt out of partitions.)
- **PACELC:** *else* (no partition) you still trade **L**atency vs **C**onsistency. This is the everyday trade-off.
- **Useful middle grounds:** read-your-writes, monotonic reads, causal consistency.
- **Practical rule:** **strong where money or unique inventory is involved; eventual elsewhere.**
- **Say:** "Seat state is strongly consistent; the seat map on the edge is eventually consistent and I verify at hold time."

## 9. Queues and streams
| | **Queue** (SQS, Service Bus, RabbitMQ) | **Log / stream** (Kafka, Event Hubs) |
|---|---|---|
| Model | Message consumed then gone | Append-only log, consumers track offsets |
| Replay | Usually no | Yes |
| Ordering | Per queue / FIFO option | Per **partition** |
| Fan-out | Via topics/subscriptions | Many consumer groups |
| Use | Task work, decoupling | Event streams, analytics, replay |

- **Delivery guarantees:** at-most-once (may lose), **at-least-once** (may duplicate; the practical default), exactly-once (really = at-least-once + idempotent consumers).
- **Dead-letter queue** for poison messages; **backpressure** so producers cannot overwhelm consumers; **partition key** keeps related messages ordered.
- **Why queues:** absorb spikes, decouple slow work (email, thumbnails), retry safely.
- **Say:** "At-least-once delivery with an idempotent consumer, deduplicating on the event id."

## 10. Idempotency
- **Definition:** doing it twice has the same effect as once. Essential because networks retry.
- **Naturally idempotent:** PUT, DELETE, "set X = 5".
- **Not idempotent:** POST "charge $10", "increment".
- **Technique:** client sends an **idempotency key**; server stores `key → result`; a replay returns the stored result.
- **Race on the key:** use a unique constraint or `GetOrAdd`-with-lazy so two simultaneous requests run the work once.
- **Say:** "Every checkout has a unique key; the payment service stores the outcome per key."

## 11. Transactions across services
You cannot have one ACID transaction across a database and a queue, or two services.

| Pattern | Idea | Use |
|---|---|---|
| **2PC** | Coordinator asks all to prepare, then commit | Rare; blocking, fragile at scale |
| **Saga** | Sequence of local transactions with **compensating** actions | Order flows, bookings |
| **Transactional outbox** | Write the event in the **same** DB transaction as the data; a relay publishes it | Reliable event publishing |
| **Reconciliation job** | Periodically compare sources of truth and fix | Safety net for everything above |

- **Dual-write bug:** "save to DB, then publish" can lose the publish if you crash between. Fix with the outbox.
- **Say:** "Outbox for events, saga with compensation for multi-step flows, and a reconciler as the safety net."

## 12. Concurrency control
| Technique | How | Use |
|---|---|---|
| **Pessimistic lock** | Lock first, others wait | Frequent conflicts, short critical sections |
| **Optimistic** | Try; detect conflict at write (version column / unique constraint); retry | Rare conflicts |
| **Atomic primitive** | `SET NX`, CAS, `INCR`, Lua script | Fast single-key decisions |
| **Unique constraint** | DB refuses a duplicate | Final safety net |
| **Serializable txn** | Strictest isolation | Correctness first, lower throughput |

- **Isolation levels** (weak → strong): read committed → repeatable read → serializable. Know that **lost update** and **write skew** are what the weaker levels allow.
- **Say:** "A short pessimistic hold takes the decision fast; an optimistic unique constraint is the backstop."

## 13. Rate limiting
- **Algorithms:** **token bucket** (allows bursts, the common default), **leaky bucket** (smooths), **fixed window** (simple, boundary burst problem), **sliding window** (accurate).
- **Where:** edge/gateway (per IP/user/API key), per service, per tenant.
- **Distributed:** a shared counter in Redis (atomic `INCR` + expiry) or Lua; accept small inaccuracy.
- **Respond:** HTTP **429** with `Retry-After`.
- **Say:** "Token bucket per user at the gateway, counters in Redis."

## 14. API gateway and service patterns
- **API gateway:** single entry point: auth, rate limiting, routing, TLS, request shaping.
- **BFF** (backend for frontend): per-client aggregation layer.
- **Microservices vs monolith:** microservices buy independent deploy/scale at the cost of network failures, data consistency and ops. **Start modular-monolith unless scale or teams demand otherwise.**
- **Service discovery, config, versioning** (additive changes, never break clients).
- **Sync vs async:** call synchronously when the user waits; use events when they don't.

## 15. Reliability patterns
- **Timeouts everywhere** (no timeout = a hung thread).
- **Retries with exponential backoff + jitter** (and only for idempotent calls).
- **Circuit breaker:** stop calling a failing dependency, fail fast, probe to recover.
- **Bulkheads:** isolate pools so one slow dependency cannot exhaust everything.
- **Load shedding & graceful degradation:** drop non-essential work first.
- **Health checks, redundancy, multi-AZ, multi-region** (state clearly which failures you survive).
- **Say:** "If payments are slow we time out, retry idempotently with backoff, and the breaker stops the pile-up."

## 16. Object storage and large files
- **Blob stores** (S3, Azure Blob): cheap, durable, flat namespace.
- **Large uploads:** **pre-signed URL** (client uploads directly to storage), **chunked/resumable** uploads, checksum per chunk.
- **Deduplicate** by content hash. **Async processing** (transcode, thumbnail) via a queue.
- **Serve** through a CDN.

## 17. Search
- **Inverted index:** term → list of documents. Elasticsearch/OpenSearch/Lucene.
- **Pipeline:** source of truth (DB) → change events → indexer → search index. The index is **derived, eventually consistent**.
- **Typeahead:** a trie or prefix index, precomputed top-N, cached.

## 18. Real-time delivery
| Technique | Notes |
|---|---|
| **Polling** | Simple, wasteful |
| **Long polling** | Fewer empty responses |
| **WebSocket** | Two-way persistent; need connection-aware routing |
| **Server-sent events** | One-way server → client |
| **Push notifications** | For offline devices |

- **Fan-out:** on write (push to followers' feeds, fast reads, heavy for celebrities) vs on read (pull, cheap writes, slow reads). Real systems **mix** the two.

## 19. ID generation
- **UUID v4:** no coordination, but large and unordered (bad for B-tree locality).
- **Snowflake-style:** timestamp + machine id + sequence; roughly time-ordered, 64-bit.
- **DB auto-increment:** simple, a bottleneck/leak at scale.
- **URL shortener:** counter → base62, or hash + collision handling.

## 20. Consensus, leader election and locks
- **Consensus (Raft/Paxos)** keeps a replicated log agreed; used inside etcd, ZooKeeper, many databases.
- **Leader election** via a lease in a consensus store.
- **Distributed lock:** a **lease** with a TTL. Beware pauses: use a **fencing token** so a stale holder cannot write after losing the lock.
- **Say:** "I'd avoid a general distributed lock; a single atomic claim with a TTL plus a database constraint is simpler and safer."

## 21. Probabilistic data structures
- **Bloom filter:** "definitely not present" or "maybe present"; saves lookups for non-existent keys.
- **HyperLogLog:** approximate distinct counts in tiny memory.
- **Count-min sketch:** approximate frequencies (top-K).

## 22. Observability
- **Metrics** (rates, errors, latency p50/p95/p99), **logs** (structured, with correlation ids), **traces** (follow one request across services).
- **Golden signals:** latency, traffic, errors, saturation.
- **SLI/SLO/error budget:** measurable target, agreed goal, allowed failure.
- **Alert on symptoms** (user-visible), not every cause. Have a **runbook**.

## 23. Security basics
- **AuthN vs AuthZ.** OAuth2/OIDC, JWT (short-lived, signed), refresh tokens.
- **TLS** in transit; **encryption at rest**; secrets in a vault, never in code.
- **Least privilege**, input validation, **rate limits**, audit logs, PII handling.
- **Abuse:** bots, scraping, credential stuffing → rate limits, challenges, device signals.

## 24. Cloud mapping (Azure-flavoured)
Useful for Microsoft-style conversations. Say the concept first, then the product as an example.

| Concept | Azure example |
|---|---|
| CDN / edge | Azure Front Door, Azure CDN |
| Load balancer / gateway | Azure Load Balancer, Application Gateway, API Management |
| Compute | App Service, AKS, Azure Functions |
| Relational DB | Azure SQL Database |
| Global NoSQL | Azure Cosmos DB |
| Cache | Azure Cache for Redis |
| Queue | Azure Service Bus, Storage Queues |
| Event streaming | Azure Event Hubs |
| Object storage | Azure Blob Storage |
| Search | Azure AI Search |
| Monitoring | Azure Monitor, Application Insights |
| Identity | Microsoft Entra ID |

> [!TIP]
> Do not recite products as a substitute for reasoning. "I need a managed cache with TTL and atomic ops, for example Redis" beats "use Azure Cache for Redis" alone.
