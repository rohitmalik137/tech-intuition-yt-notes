# Glossary

One line per term, grouped by topic so you can scan a whole area at once. Deeper explanations live in [Building blocks](fundamentals/03-building-blocks.md).

**Jump to:** [Consistency & transactions](#consistency--transactions) · [Data & storage](#data--storage) · [Scaling & distribution](#scaling--distribution) · [Messaging](#messaging) · [Reliability](#reliability) · [Coordination](#coordination) · [Caching & networking](#caching--networking) · [Measuring](#measuring) · [Design (LLD)](#design-lld)

---

## Consistency & transactions

| Term | Meaning |
|---|---|
| **ACID** | Atomic, Consistent, Isolated, Durable: the guarantees of a database transaction |
| **CAP** | During a network partition you must choose Consistency or Availability |
| **PACELC** | If Partitioned: A or C; Else: trade Latency vs Consistency |
| **Strong consistency** | Every read sees the latest write |
| **Eventual consistency** | Replicas converge given time; reads may be stale meanwhile |
| **Linearizable** | Operations appear to happen in one real-time order |
| **Read-your-writes** | After you write, your own reads see it |
| **Isolation level** | How much concurrent transactions can see of each other (read committed → serializable) |
| **Write skew** | Two transactions read, then write different rows, breaking a shared rule |
| **Optimistic concurrency** | Try, detect conflict at write time, retry |
| **Pessimistic concurrency** | Lock first so others wait |
| **Two-phase commit (2PC)** | Blocking protocol for an atomic commit across systems |
| **Saga** | Multi-step process where each step has a compensating undo |
| **Outbox** | Store the event in the same transaction as the data; publish it later |
| **Reconciliation** | Periodically comparing sources of truth to repair drift |
| **Idempotent** | Doing it twice has the same effect as once |
| **Idempotency key** | Client-supplied id so a retry returns the stored result instead of repeating work |

## Data & storage

| Term | Meaning |
|---|---|
| **Index** | Extra structure that speeds reads at a cost to writes |
| **B-tree** | Default ordered index; good for equality and ranges |
| **LSM tree** | Write-optimised storage structure (used by Cassandra, RocksDB) |
| **WAL** | Write-ahead log: written before applying a change, for crash recovery |
| **Denormalisation** | Storing duplicate data to make reads cheaper |
| **CQRS** | Separate models for reading and writing |
| **Object storage** | Cheap, durable blob store (S3, Azure Blob) |
| **Inverted index** | Maps term → documents; powers search |
| **Time-series DB** | Store optimised for timestamped metrics |
| **CRDT** | Data type whose concurrent edits merge without conflicts |

## Scaling & distribution

| Term | Meaning |
|---|---|
| **Sharding** | Splitting data across nodes |
| **Partition key** | Decides which shard a record lives on |
| **Consistent hashing** | Ring-based hashing so adding a node moves few keys |
| **Hot partition / hot key** | One shard or key receiving disproportionate load |
| **Replication** | Keeping copies of data on several nodes |
| **Read replica** | Read-only copy of a database |
| **Replication lag** | Delay before a replica has the latest write |
| **Leader–follower** | One node takes writes; others copy and serve reads |
| **Quorum** | Majority needed for a read/write (R + W > N) |
| **Fan-out** | Delivering one event to many targets |
| **Load balancer** | Spreads requests across servers |
| **Stateless service** | Keeps no per-user state, so any instance can serve any request |

## Messaging

| Term | Meaning |
|---|---|
| **Queue** | Messages consumed once, then removed |
| **Stream / log** | Append-only record consumers read by offset; replayable |
| **At-most-once** | May lose messages, never duplicates |
| **At-least-once** | Never loses, may duplicate (the practical default) |
| **Exactly-once** | In practice: at-least-once plus idempotent consumers |
| **DLQ (dead-letter queue)** | Where messages that keep failing are parked |
| **Backpressure** | Slowing producers when consumers can't keep up |
| **Pub/sub** | Publishers emit to topics; many subscribers receive |

## Reliability

| Term | Meaning |
|---|---|
| **Timeout** | Give up waiting after a set time |
| **Retry with backoff + jitter** | Retry with growing, randomised delays |
| **Circuit breaker** | Stop calling a failing dependency; fail fast; probe to recover |
| **Bulkhead** | Isolate resources so one failure can't spread |
| **Graceful degradation** | Drop non-essential features to keep the core working |
| **Load shedding** | Deliberately rejecting some work to protect the system |
| **Single point of failure (SPOF)** | One component whose failure takes everything down |
| **Failover** | Switching to a standby when the primary fails |
| **Split brain** | Two nodes both believe they are the leader |

## Coordination

| Term | Meaning |
|---|---|
| **Consensus (Raft / Paxos)** | Nodes agreeing on a replicated log |
| **Leader election** | Choosing one node to coordinate |
| **Lease** | Time-limited ownership or lock |
| **Fencing token** | Increasing number so a stale lock holder can't write |
| **Gossip protocol** | Nodes spread state peer to peer |
| **Vector clock** | Tracks causality between versions |
| **Distributed lock** | Mutual exclusion across servers (usually a lease) |

## Caching & networking

| Term | Meaning |
|---|---|
| **CDN** | Edge network caching content near users |
| **Cache-aside** | App reads cache, falls back to the DB and fills the cache |
| **Write-through / write-behind** | Write cache and DB together / cache first, DB later |
| **TTL** | Time to live before an entry expires |
| **Eviction (LRU / LFU)** | Which entry to drop when full: least recently / least frequently used |
| **Cache stampede** | Many requests rebuild a just-expired hot key at once |
| **Bloom filter** | Compact set that answers "definitely not" or "maybe" |
| **API gateway** | Single entry point: auth, rate limit, routing |
| **Rate limiting** | Capping requests per time window (token bucket, sliding window) |
| **WebSocket** | Persistent two-way connection |

## Measuring

| Term | Meaning |
|---|---|
| **Latency / throughput** | Time per request / requests per second |
| **p50 / p95 / p99** | Latency under which 50 / 95 / 99% of requests finish |
| **SLI** | A measurement (e.g. % of requests under 200 ms) |
| **SLO** | The target for an SLI |
| **Error budget** | How much failure the SLO allows |
| **Availability "nines"** | 99.9% ≈ 8.8 hours of downtime a year; 99.99% ≈ 52 minutes |
| **Golden signals** | Latency, traffic, errors, saturation |
| **Trace / metric / log** | One request's path / a number over time / an event record |

## Design (LLD)

| Term | Meaning |
|---|---|
| **SOLID** | Five design principles: single responsibility, open/closed, Liskov, interface segregation, dependency inversion |
| **Composition over inheritance** | Build behaviour by combining objects, not by deep class trees |
| **Dependency injection** | Pass collaborators in (as interfaces) instead of creating them inside |
| **Strategy** | Swap interchangeable algorithms behind one interface |
| **State** | Object's behaviour changes with its state; each state is a class |
| **Observer** | One event, many subscribers |
| **Decorator** | Wrap an object to add behaviour |
| **Factory** | Centralise how objects get created |
| **Facade** | Simple front for a complex subsystem |
| **Invariant** | A rule that must always hold (e.g. a seat is never booked twice) |
| **Check-then-act race** | Two threads both pass the check before either acts |
| **Deadlock** | Threads wait on each other forever; prevent with a global lock order |
