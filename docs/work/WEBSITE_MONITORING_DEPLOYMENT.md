# Website Monitoring deployment notes

The feature must default disabled until configured. Existing Monitor deployments and P0 acceptance must remain behaviorally unchanged when Website Monitoring is disabled.

Production activation requires explicit configuration for:

- feature enabled flag;
- approved outbound destination/private-range policy;
- scheduler limits;
- target registrations;
- incident thresholds;
- recipient groups;
- SMTP host/port/TLS policy;
- SMTP credential secret reference when authentication is required.

Upgrade packages must preserve existing `App_Data` and environment-specific settings according to the established Monitor deployment contract. Website target/outbox stores belong under the stable operational state boundary, not inside replaceable application binaries.
