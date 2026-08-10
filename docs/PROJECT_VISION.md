# Project Vision

Monitor is a professional SQL Server operations center for DBAs and infrastructure teams.

The core product question is: **What needs my attention right now?**

The product should feel like a modern mission-control interface without becoming game-like. It must communicate system activity with controlled motion, clear health states, strong information hierarchy, and low operational overhead.

## Principles

- Visible product increments over backend-only development.
- Snapshot-first monitoring and centralized collection.
- One central live experience on the SQL Command Center.
- No UI animation may cause SQL traffic.
- Real production data must never be confused with demo data.
- Recommendations must eventually explain both the detected problem and a concrete remediation path/query.
- AI is advisory: it receives normalized evidence and returns suggestions; it does not autonomously execute SQL.
