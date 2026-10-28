# Numbers and back-of-envelope estimation

You do not need exact numbers. You need the **shape**: is this a storage problem, a throughput problem, or a contention problem?

## Memorise these

**Time & size**

| Thing | Approx value |
|---|---|
| Seconds in a day | ~86,400 → **~100,000** (round up for easy maths) |
| Seconds in a month | ~2.5 million |
| 1 KB / 1 MB / 1 GB / 1 TB | 10³ / 10⁶ / 10⁹ / 10¹² bytes |
| Powers of two | 2¹⁰≈1 thousand, 2²⁰≈1 million, 2³⁰≈1 billion |

**Latency (order of magnitude, commonly cited)**

| Operation | Roughly |
|---|---|
| L1 cache / main memory read | ~1 ns / ~100 ns |
| SSD random read | ~100 µs |
| Network round trip, same data centre | ~0.5 ms |
| Read 1 MB from SSD / from memory | ~1 ms / ~0.25 ms |
| Disk seek (spinning) | ~10 ms |
| Round trip across continents | ~150 ms |

**Takeaways:** memory is ~1000× faster than SSD for random access; a cross-region call costs ~100× an in-DC call; **avoid chatty cross-region calls**.

**Throughput (rough, single node; depends heavily on hardware and workload)**

| Component | Order of magnitude |
|---|---|
| Redis (simple ops) | ~100K ops/s |
| A typical relational DB (OLTP) | ~1K–10K writes/s |
| A typical web server | ~1K–10K requests/s |
| Kafka partition | ~10K–100K+ msgs/s |

## The estimation recipe

1. **Users → requests.** `QPS = users × actions per user per day ÷ 100,000`
2. **Peak = 2–10× average** (or the spike you were told about).
3. **Storage = items × size × retention.** Add replication (×3).
4. **Bandwidth = QPS × payload size.**
5. **Cache size ≈ the hot 20% of the data** (a rule of thumb, not a law).
6. **Read:write ratio** decides caching vs queueing.

### Worked example (from the ticket-booking note)

- 13,000,000 fans, 150,000 seats → **87 fans per seat**.
- If checkout can safely handle 2,000/s → sell-out takes `150,000 ÷ 2,000 = 75 s`.
- Hold storage: 2,000/s × 480 s TTL ≈ 1M live holds × ~100 B ≈ **100 MB**. Fits in one Redis.
- Booking rows: 150,000 × ~200 B ≈ 30 MB.

**Conclusion to say out loud:** the data is tiny. The problem is **contention and a spike**, not storage. *A good estimate points you at the real problem.*

## Availability in "nines"

| Availability | Downtime per year |
|---|---|
| 99% | ~3.65 days |
| 99.9% | ~8.8 hours |
| 99.99% | ~52 minutes |
| 99.999% | ~5 minutes |

Chained services multiply: two 99.9% services in series ≈ 99.8%.

## Habits

- Round aggressively; say your assumptions out loud.
- Write units on every line.
- Sanity-check against something you know ("does 1 TB/day sound plausible?").
- Stop estimating the moment the answer is clear.
