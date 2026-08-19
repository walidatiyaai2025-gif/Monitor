# Website Monitoring notification examples (content contract)

## Incident opened

Subject shape: `[Monitor][Critical][Production] Website down — <target>`

Body should contain: incident id, target, environment, observed time, sanitized URL/host, classification, probable layer, bounded supporting evidence, current consecutive failures, response/status/certificate detail when relevant, and a link/path back to the Monitor incident UI when available.

## Recovery

Subject shape: `[Monitor][Recovered][Production] Website restored — <target>`

Body should contain: incident id, target, recovered time, outage duration based on incident timestamps, final successful status/latency, and the previous failure classification.

These are shapes only. Final HTML/text templates must HTML-encode all target/evidence values and must never embed secret-bearing response data.
