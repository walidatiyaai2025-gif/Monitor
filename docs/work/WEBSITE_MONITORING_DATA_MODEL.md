# Website Monitoring conceptual data model

## WebsiteTarget

- `Id` GUID
- `Name` bounded display name
- `Url` sanitized absolute HTTP/HTTPS URL
- `Environment`
- `IsEnabled`
- `IntervalSeconds`
- `TimeoutSeconds`
- `ExpectedStatusMin` / `ExpectedStatusMax`
- optional bounded `ExpectedContentMarker`
- `FollowRedirects` / `ExpectedFinalHost`
- `SlowThresholdMilliseconds`
- `FailureConfirmationCount`
- `RecoveryConfirmationCount`
- bounded tags / owner / service metadata
- notification group ids
- created/updated audit timestamps

## WebsiteProbeResult

- target id
- started/completed UTC
- normalized classification rule
- success/degraded/down/unknown state
- DNS duration + sanitized resolved-address summary
- TCP duration/result
- TLS duration/result + certificate subject/issuer/notBefore/notAfter/thumbprint hash metadata as policy permits
- HTTP duration/status/final URI host/redirect count
- content-marker result
- total elapsed
- bounded non-secret evidence summary

## WebsiteCheckState

Durable per-target rolling state for consecutive failure/success confirmation, last success/failure, active classification, last-notified incident/version and cooldown timestamps.

## WebsiteCheckHistory

Bounded retained probe summaries suitable for UI/SLA calculations; never retain full response bodies or secret-bearing headers.

## NotificationRecipientGroup

- id/name
- enabled
- bounded unique email addresses
- optional environment/severity applicability

## NotificationOutboxItem

Durable bounded message intent containing incident/target/version/dedup identity and sanitized rendered payload; credential material is not part of the outbox.
