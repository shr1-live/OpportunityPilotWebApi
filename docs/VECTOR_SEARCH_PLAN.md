# Vector search plan

Status: architecture approved for implementation; no schema migration or embedding provider is enabled yet.

## Decision

Use the existing PostgreSQL database with the `pgvector` extension. Do not add a separate vector-database service for
the first rollout. Operational entities remain the source of truth; vectors are a rebuildable retrieval index.

Vector similarity proposes candidates for review. Deterministic hard filters, evidence coverage, ownership, approval
and provider policy remain authoritative. A similarity result can never send a message, submit a bid, apply to a job,
share a candidate or advance a deal by itself.

## Valuable first use cases

1. Rank job descriptions against a confirmed Candidate profile after hard location, work-mode, age and exclusion rules.
2. Rank buyer/project evidence against a Sales campaign, service offer and ideal-customer description.
3. Retrieve the best company candidates for a confirmed client requirement while enforcing consent and share settings.
4. Retrieve supporting evidence for a draft or explanation and cite the exact source record/chunk.
5. Detect near-duplicate opportunities or company requirements as an advisory signal alongside normalized identifiers.

## Storage model

Use one owner-scoped semantic index rather than embedding columns scattered across every operational table:

```text
SemanticChunk
  Id
  OwnerId
  Workspace             Candidate | Sales
  EntityType            Profile | Campaign | Evidence | Opportunity | Requirement | CandidateSubmission
  EntityId
  SourceEvidenceId?     required when the text came from external evidence
  ChunkOrdinal
  Content               bounded text used to create the embedding
  ContentHash            prevents duplicate work and detects stale embeddings
  EmbeddingModel
  EmbeddingDimensions
  Embedding              vector or halfvec; dimension fixed by the chosen model
  MetadataJson           non-authoritative filter/display metadata
  CreatedAt
  UpdatedAt
  ExpiresAt?
```

The database migration must enable `vector` without pinning an extension version. Supabase now ignores explicit
extension-version pins, so the installed default must be inspected during release. If tables are exposed through the
Supabase Data API, access must be explicitly granted and RLS must also be enabled; the preferred design is API-only
access in a non-exposed schema.

## Retrieval and scoring

1. Apply owner/workspace scope and hard business filters in SQL before ranking.
2. Generate the query embedding with the same model/version used by stored chunks.
3. Retrieve a bounded top-K using cosine distance.
4. Join every result back to its current operational entity and evidence; discard deleted, expired or stale versions.
5. Combine normalized semantic similarity with deterministic rule score and evidence coverage.
6. Present both parts separately: rule score, semantic relevance, supporting evidence and missing criteria.
7. Require the existing approval flow for every downstream action.

Suggested initial blend for evaluation only: `70% deterministic/evidence score + 30% semantic similarity`. Tune this
against a labelled set; do not promote it directly to production policy.

For a small corpus, start with exact cosine search and measured query plans. Add HNSW only when row count and latency
justify approximate search. When indexed, order directly by the distance operator so PostgreSQL can use the index.

## Embedding pipeline

- Enqueue only after a source record is saved and eligible for search.
- Build canonical, bounded text from approved fields plus attributable evidence.
- Use `ContentHash + EmbeddingModel` as the idempotency identity.
- Generate asynchronously in bounded batches; store retry count and final failure without blocking the main workflow.
- Re-embed only when canonical content or model changes.
- Delete or expire vectors with the owning source and support full index rebuilds.
- Keep API behaviour usable when the embedding worker/provider is unavailable; fall back to deterministic ranking.

Supabase can implement this with pgvector plus an asynchronous queue/worker. Its documented automatic-embedding pattern
uses triggers, `pgmq`, `pg_cron`, `pg_net` and an Edge Function, but the current Render API already has background-worker
infrastructure. Choose one owner for the pipeline to avoid duplicate jobs; the recommended first implementation is the
API worker with PostgreSQL as the queue/source of truth, then reassess managed Supabase queues if needed.

## Tenant, privacy and security controls

- Every query includes the authenticated owner (and later organization) predicate before distance ranking.
- Do not authorize from user-editable JWT metadata.
- Do not embed secrets, private notes, access tokens, raw resumes or candidate fields lacking consent.
- Store external text with its source URL/evidence id and retention class.
- A candidate search result does not make a field shareable; submission permissions are checked again at packaging.
- Keep service-role/secret keys server-side and never return raw embeddings to the browser.
- Log model/version, source entity/version and result identifiers, not sensitive embedding inputs.

## Rollout

1. Label 50–100 representative Candidate and Sales matches with relevant/not-relevant decisions.
2. Select one embedding model and dimension; record cost, latency, region and data-processing terms.
3. Add the extension and owner-scoped semantic schema through one reviewed provider-specific migration.
4. Add the asynchronous idempotent embedding job and deterministic fallback.
5. Ship shadow retrieval: collect relevance metrics without changing visible rank.
6. Compare precision/recall and false-positive patterns with the current rules.
7. Show semantic relevance and citations behind a feature flag.
8. Enable hybrid ranking gradually, monitor latency/cost, and retain a one-switch rollback to rules-only ranking.

## Decisions required before implementation

- Embedding provider/model and data residency.
- Maximum retained source text and candidate-data consent language.
- Whether Supabase Edge Functions or the existing Render worker owns embedding generation.
- Labelled evaluation set and acceptable relevance/latency targets.
- Corpus size at which HNSW becomes justified.

## Current official references

- Supabase vector columns: https://supabase.com/docs/guides/ai/vector-columns
- Supabase vector indexes: https://supabase.com/docs/guides/ai/vector-indexes
- Supabase automatic embeddings: https://supabase.com/docs/guides/ai/automatic-embeddings
- Supabase extension version change: https://supabase.com/changelog/extension-version-pinning-ignored
